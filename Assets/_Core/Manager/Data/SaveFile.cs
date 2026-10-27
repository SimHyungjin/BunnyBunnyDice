using System;
using System.IO;
using System.IO.Compression;

/// <summary>
/// 세이브 파일 한 개의 형식과 디스크 입출력. 섹션의 내용(JSON·바이너리)은 모르고, 바이트 묶음만 다룬다.
///
/// 형식: [헤더 26바이트][데이터]
///   magic(4) · 섹션 형식(1) · 플래그(1) · 섹션 버전(4) · 수정 시각 UTC ticks(8) · 데이터 길이(4) · CRC32(4)
/// 데이터는 플래그에 따라 GZip 압축 후 XOR로 섞는다. CRC는 디스크에 놓인 데이터 그대로에 대해 계산한다.
///
/// 난독화는 "파일을 열어 숫자를 고치는" 수준을 막는 것이 목적이다. 클라이언트에 키가 있으니 보안은 아니다.
/// 에디터에서는 섞지 않고 평문으로 써서 파일을 열어 볼 수 있게 한다. 읽기는 플래그를 따르므로 둘 다 읽힌다.
///
/// 쓰기는 원자적으로 한다: 임시 파일에 다 쓴 뒤 기존 파일을 .bak으로 옮기고 임시 파일을 제자리에 놓는다.
/// 쓰는 도중 앱이 죽어도 기존 파일이나 .bak 중 하나는 온전하다. 읽기는 원본 → 임시 → 백업 순으로 체크섬이 맞는 것을 쓴다.
/// </summary>
internal static class SaveFile
{
    public const string Extension = ".sav";

    public enum Format : byte
    {
        Json = 0,
        Binary = 1,
    }

    [Flags]
    private enum Flags : byte
    {
        None = 0,
        GZip = 1 << 0,
        Xor  = 1 << 1,
    }

    public readonly struct Contents
    {
        public readonly Format Format;
        public readonly int Version;
        public readonly long ModifiedUtcTicks;
        public readonly byte[] Payload;

        public Contents(Format format, int version, long modifiedUtcTicks, byte[] payload)
        {
            Format = format;
            Version = version;
            ModifiedUtcTicks = modifiedUtcTicks;
            Payload = payload;
        }
    }

    private const uint Magic = 0x31565344;   // "DSV1"
    private const int HeaderSize = 4 + 1 + 1 + 4 + 8 + 4 + 4;

    // 에디터는 평문, 빌드는 압축 + 섞기. (const로 두면 한쪽 분기가 "도달할 수 없는 코드" 경고를 낸다)
#if UNITY_EDITOR
    private static readonly Flags WriteFlags = Flags.None;
#else
    private static readonly Flags WriteFlags = Flags.GZip | Flags.Xor;
#endif

    private static readonly byte[] XorKey =
    {
        0x5B, 0xC1, 0x2E, 0x97, 0x6A, 0xF3, 0x08, 0xD4,
        0x3F, 0x81, 0xBA, 0x14, 0x6C, 0xE9, 0x45, 0x2D,
    };

    #region Encode / Decode

    public static byte[] Encode(Format format, int version, byte[] payload)
    {
        byte[] data = payload;
        if ((WriteFlags & Flags.GZip) != 0) data = Compress(data);
        if ((WriteFlags & Flags.Xor) != 0)  data = Xor(data);

        using var stream = new MemoryStream(HeaderSize + data.Length);
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Magic);
            writer.Write((byte)format);
            writer.Write((byte)WriteFlags);
            writer.Write(version);
            writer.Write(DateTime.UtcNow.Ticks);
            writer.Write(data.Length);
            writer.Write(Crc32(data));
            writer.Write(data);
        }
        return stream.ToArray();
    }

    /// <summary>파일 바이트를 검증하고 풀어 낸다. 형식이 다르거나 체크섬이 맞지 않으면 false.</summary>
    public static bool TryDecode(byte[] bytes, out Contents contents)
    {
        contents = default;
        if (bytes == null || bytes.Length < HeaderSize) return false;

        try
        {
            using var reader = new BinaryReader(new MemoryStream(bytes));
            if (reader.ReadUInt32() != Magic) return false;

            var format = (Format)reader.ReadByte();
            var flags = (Flags)reader.ReadByte();
            int version = reader.ReadInt32();
            long modified = reader.ReadInt64();
            int length = reader.ReadInt32();
            uint crc = reader.ReadUInt32();

            if (length < 0 || HeaderSize + length != bytes.Length) return false;

            byte[] data = reader.ReadBytes(length);
            if (Crc32(data) != crc) return false;

            if ((flags & Flags.Xor) != 0)  data = Xor(data);
            if ((flags & Flags.GZip) != 0) data = Decompress(data);

            contents = new Contents(format, version, modified, data);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    #endregion

    #region Disk

    /// <summary>
    /// 섹션 파일을 읽는다. 원본이 깨졌으면 임시 파일 → 백업 순으로 시도한다.
    /// 원본이 있었는데 깨져 있었다면 .corrupt로 남겨 둔다(원인 조사와 수동 복구용).
    /// </summary>
    /// <param name="recovered">원본이 아닌 임시·백업 파일에서 읽었는지. true면 호출자가 원본을 다시 써야 한다.</param>
    /// <returns>읽은 것이 있으면 true. 파일이 하나도 없거나 모두 깨졌으면 false.</returns>
    public static bool TryRead(string path, out Contents contents, out bool recovered)
    {
        recovered = false;

        bool mainExists = File.Exists(path);
        if (mainExists && TryDecode(ReadBytes(path), out contents)) return true;

        if (TryDecode(ReadBytes(TempPath(path)), out contents) || TryDecode(ReadBytes(BackupPath(path)), out contents))
        {
            recovered = true;
            if (mainExists) KeepCorrupt(path);
            Debug.LogWarning($"[SaveFile] '{Path.GetFileName(path)}'이(가) 손상되었거나 없어 임시·백업 파일에서 복구했습니다.");
            return true;
        }

        if (mainExists)
        {
            KeepCorrupt(path);
            Debug.LogError($"[SaveFile] '{Path.GetFileName(path)}'이(가) 손상되었고 복구할 백업이 없습니다. 손상된 파일은 .corrupt로 보존했습니다.");
        }

        contents = default;
        return false;
    }

    /// <summary>원자적으로 쓴다. 실패하면 예외를 던진다(호출자가 dirty를 유지해 다음에 다시 시도한다).</summary>
    public static void WriteAtomic(string path, byte[] bytes)
    {
        string temp = TempPath(path);
        string backup = BackupPath(path);

        // Flush(true)로 디스크까지 내려보낸다. 그래야 아래 이름 바꾸기 뒤에 내용이 비어 있는 일이 없다.
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }

        if (File.Exists(path))
        {
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(path, backup);
        }

        File.Move(temp, path);
    }

    /// <summary>섹션 파일과 임시·백업 파일을 지운다. (.corrupt는 남긴다)</summary>
    public static void Delete(string path)
    {
        TryDelete(path);
        TryDelete(TempPath(path));
        TryDelete(BackupPath(path));
    }

    private static string TempPath(string path) => path + ".tmp";
    private static string BackupPath(string path) => path + ".bak";

    private static byte[] ReadBytes(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void KeepCorrupt(string path)
    {
        try
        {
            File.Copy(path, path + ".corrupt", true);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveFile] 손상 파일 보존 실패: {e.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveFile] 삭제 실패 '{Path.GetFileName(path)}': {e.Message}");
        }
    }

    #endregion

    #region Transform

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest))
            gzip.Write(data, 0, data.Length);
        return output.ToArray();
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    // 같은 연산을 두 번 하면 원래대로 돌아온다. 위치를 섞어 키가 반복되는 패턴이 덜 드러나게 한다.
    private static byte[] Xor(byte[] data)
    {
        var result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
            result[i] = (byte)(data[i] ^ XorKey[i % XorKey.Length] ^ (byte)(i * 31));
        return result;
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFFu;
        for (int i = 0; i < data.Length; i++)
            crc = CrcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    #endregion
}

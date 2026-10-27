using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 세이브를 섹션(<see cref="SaveSection"/>) 단위로 관리한다. 섹션 하나가 파일 하나다. (persistentDataPath/Save)
///
/// 섹션은 처음 <see cref="Get{T}"/>할 때 로드되고(없으면 기본값으로 생성), 값을 바꾼 쪽이 <see cref="SaveSection.MarkDirty"/>한다.
/// 저장은 바뀐 섹션만 한다. 저장 시점은 셋이다.
///  - 백그라운드 전환·종료: 앱이 곧 죽을 수 있으므로 반드시 그 자리에서(동기) 저장한다.
///  - 씬 전환: Core.Clear에서.
///  - 자동 저장: <see cref="SO_CoreConfig.DataAutoSaveInterval"/>마다. MarkDirty 이후 앱이 비정상 종료돼도 잃는 범위를 줄인다.
///
/// 파일이 깨지면 백업에서 복구하고, 복구할 수 없으면 손상 파일을 보존한 채 기본값으로 시작한다(조용히 덮어쓰지 않는다).
/// 클라우드 동기화용으로 섹션 단위·전체 단위 내보내기/가져오기를 제공한다 — 실제 업로드는 서비스 레이어가 한다.
/// </summary>
public sealed class DataManager : Manager
{
    private readonly Dictionary<Type, SaveSection> _byType = new();
    private readonly Dictionary<string, SaveSection> _byKey = new();
    private float _autoSaveElapsed;

    public static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Save");

    /// <summary>
    /// 섹션 내용이 통째로 바뀌었을 때(초기화·가져오기). 값을 캐시해 둔 쪽은 여기서 다시 읽는다 — 예: 설정을 각 매니저에 다시 적용.
    /// 섹션 인스턴스는 그대로이고 내용만 바뀐다.
    /// </summary>
    public event Action OnDataReplaced;

    public override Type[] Dependencies => new[] { typeof(AppStateManager), typeof(LoopManager) };

    protected override UniTask OnInitializeAsync()
    {
        Directory.CreateDirectory(SaveDirectory);

        // OnEnterBackground가 아니라 OnBackgroundRaw에 거는 이유: 광고·결제창이 떠 있는 동안
        // OS가 앱을 종료할 수 있는데, 억제를 존중하다 저장을 놓치면 그 세션이 통째로 사라진다.
        // 저장은 헛돌아도 손해가 없지만 놓치면 되돌릴 수 없다.
        Core.AppState.OnBackgroundRaw += SaveDirty;
        Core.Loop.OnUpdate += TickAutoSave;

        return UniTask.CompletedTask;
    }

    #region Section

    /// <summary>섹션을 가져온다. 처음이면 파일에서 로드하고, 없으면 기본값으로 만든다. 이후 같은 인스턴스를 돌려준다.</summary>
    public T Get<T>() where T : SaveSection, new()
    {
        if (_byType.TryGetValue(typeof(T), out SaveSection existing)) return (T)existing;

        var section = new T();
        string key = section.Key;

        if (_byKey.TryGetValue(key, out SaveSection clash))
        {
            Debug.LogError($"[DataManager] 섹션 키 '{key}'가 {clash.GetType().Name}와(과) {typeof(T).Name}에서 겹칩니다. Key를 다르게 지정하세요.");
            return section;
        }

        LoadInto(section);

        _byType[typeof(T)] = section;
        _byKey[key] = section;
        return section;
    }

    private void LoadInto(SaveSection section)
    {
        string path = PathOf(section.Key);
        if (!SaveFile.TryRead(path, out SaveFile.Contents contents, out bool recovered)) return;

        try
        {
            SectionCodec.Populate(section, contents.Format, contents.Payload, contents.Version);
        }
        catch (Exception e)
        {
            // 형식은 맞는데 내용을 해석할 수 없다. 기본값으로 시작하되, 원본은 보존해 둔다.
            Debug.LogError($"[DataManager] '{section.Key}' 섹션을 읽지 못해 기본값으로 시작합니다. 원본은 .corrupt로 보존합니다.\n{e}");
            TryKeepCorrupt(path);
            SectionCodec.ResetToDefaults(section);
            return;
        }

        if (contents.Version < section.Version)
        {
            section.Migrate(contents.Version);
            section.MarkDirty();
        }

        // 백업에서 살렸으면 원본을 바로 다시 써 둔다.
        if (recovered) section.MarkDirty();
    }

    #endregion

    #region Save

    /// <summary>바뀐 섹션만 지금 저장한다.</summary>
    public void SaveDirty()
    {
        foreach (SaveSection section in _byKey.Values)
            if (section.IsDirty) Write(section);

        _autoSaveElapsed = 0f;
    }

    /// <summary>로드된 모든 섹션을 지금 저장한다. (바뀌지 않았어도)</summary>
    public void SaveAll()
    {
        foreach (SaveSection section in _byKey.Values) Write(section);
        _autoSaveElapsed = 0f;
    }

    private static void Write(SaveSection section)
    {
        try
        {
            byte[] payload = SectionCodec.Serialize(section);
            byte[] file = SaveFile.Encode(SectionCodec.FormatOf(section), section.Version, payload);
            SaveFile.WriteAtomic(PathOf(section.Key), file);
            section.IsDirty = false;
        }
        catch (Exception e)
        {
            // dirty를 남겨 두면 다음 저장 시점에 다시 시도한다.
            Debug.LogError($"[DataManager] '{section.Key}' 저장 실패: {e.Message}");
        }
    }

    private void TickAutoSave(float dt)
    {
        float interval = SO_CoreConfig.Instance.DataAutoSaveInterval;
        if (interval <= 0f) return;

        _autoSaveElapsed += dt;
        if (_autoSaveElapsed >= interval) SaveDirty();
    }

    #endregion

    #region Reset

    /// <summary>섹션 하나를 기본값으로 되돌리고 파일을 지운다.</summary>
    public void Reset<T>() where T : SaveSection, new()
    {
        T section = Get<T>();
        SaveFile.Delete(PathOf(section.Key));
        SectionCodec.ResetToDefaults(section);
        section.IsDirty = false;

        RaiseReplaced();
    }

    /// <summary>모든 세이브 파일을 지우고, 로드된 섹션을 기본값으로 되돌린다.</summary>
    public void ResetAll()
    {
        foreach (string path in Directory.GetFiles(SaveDirectory, "*" + SaveFile.Extension))
            SaveFile.Delete(path);

        foreach (SaveSection section in _byKey.Values)
        {
            SectionCodec.ResetToDefaults(section);
            section.IsDirty = false;
        }

        RaiseReplaced();
    }

    #endregion

    #region Export / Import (클라우드 동기화용)

    private const uint BundleMagic = 0x41565344;   // "DSVA"

    /// <summary>섹션 파일의 마지막 저장 시각(UTC). 파일이 없거나 읽을 수 없으면 null.</summary>
    public DateTime? GetModifiedUtc(string key)
    {
        if (!SaveFile.TryRead(PathOf(key), out SaveFile.Contents contents, out _)) return null;
        return new DateTime(contents.ModifiedUtcTicks, DateTimeKind.Utc);
    }

    /// <summary>섹션 하나를 파일 그대로(헤더·체크섬 포함) 내보낸다. 바뀐 내용이 있으면 먼저 저장한다. 없으면 null.</summary>
    public byte[] ExportSection(string key)
    {
        if (_byKey.TryGetValue(key, out SaveSection section) && section.IsDirty) Write(section);

        string path = PathOf(key);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    /// <summary>모든 섹션 파일을 하나로 묶어 내보낸다. 바뀐 내용이 있으면 먼저 저장한다.</summary>
    public byte[] ExportAll()
    {
        SaveDirty();

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            string[] paths = Directory.GetFiles(SaveDirectory, "*" + SaveFile.Extension);
            writer.Write(BundleMagic);
            writer.Write(paths.Length);

            foreach (string path in paths)
            {
                byte[] bytes = File.ReadAllBytes(path);
                writer.Write(Path.GetFileNameWithoutExtension(path));
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
        }
        return stream.ToArray();
    }

    /// <summary>
    /// 섹션 하나를 받아 덮어쓴다. 체크섬이 맞지 않으면 아무것도 바꾸지 않고 false.
    /// 성공하면 그 섹션이 로드돼 있을 경우 내용을 갈아 끼우고 <see cref="OnDataReplaced"/>를 알린다.
    /// </summary>
    public bool ImportSection(string key, byte[] fileBytes)
    {
        if (!SaveFile.TryDecode(fileBytes, out _))
        {
            Debug.LogError($"[DataManager] 가져온 '{key}' 섹션이 손상되어 적용하지 않습니다.");
            return false;
        }

        if (!Apply(key, fileBytes)) return false;

        RaiseReplaced();
        return true;
    }

    /// <summary>
    /// <see cref="ExportAll"/>로 묶은 데이터를 받아 덮어쓴다. 하나라도 손상됐으면 <b>아무것도 바꾸지 않고</b> false.
    /// 묶음에 없는 로컬 섹션은 그대로 둔다.
    /// </summary>
    public bool ImportAll(byte[] bundle)
    {
        var entries = new List<(string Key, byte[] Bytes)>();

        try
        {
            using var reader = new BinaryReader(new MemoryStream(bundle), Encoding.UTF8);
            if (reader.ReadUInt32() != BundleMagic) throw new InvalidDataException("묶음 형식이 아닙니다.");

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string key = reader.ReadString();
                byte[] bytes = reader.ReadBytes(reader.ReadInt32());
                if (!SaveFile.TryDecode(bytes, out _)) throw new InvalidDataException($"'{key}' 섹션이 손상되었습니다.");
                entries.Add((key, bytes));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] 가져온 세이브를 적용하지 않습니다: {e.Message}");
            return false;
        }

        bool allApplied = true;
        foreach ((string key, byte[] bytes) in entries)
            allApplied &= Apply(key, bytes);

        RaiseReplaced();
        return allApplied;
    }

    // 파일을 쓰고, 로드된 섹션이면 그 내용으로 갈아 끼운다.
    private bool Apply(string key, byte[] fileBytes)
    {
        try
        {
            SaveFile.WriteAtomic(PathOf(key), fileBytes);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] '{key}' 섹션 쓰기 실패: {e.Message}");
            return false;
        }

        if (_byKey.TryGetValue(key, out SaveSection section))
        {
            SectionCodec.ResetToDefaults(section);
            LoadInto(section);
            section.IsDirty = false;
        }
        return true;
    }

    #endregion

    private void RaiseReplaced()
    {
        try
        {
            OnDataReplaced?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] 데이터 교체 알림 처리 중 예외가 발생했습니다.\n{e}");
        }
    }

    private static string PathOf(string key) => Path.Combine(SaveDirectory, key + SaveFile.Extension);

    private static void TryKeepCorrupt(string path)
    {
        try
        {
            if (File.Exists(path)) File.Copy(path, path + ".corrupt", true);
        }
        catch (Exception)
        {
            // 보존은 최선을 다할 뿐이다. 실패해도 진행은 막지 않는다.
        }
    }

    // 씬 전환 시 변경분 저장. (구독은 유지 — 씬을 넘어 살아있는 매니저)
    public override void Clear() => SaveDirty();
}

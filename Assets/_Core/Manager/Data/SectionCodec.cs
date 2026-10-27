using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

/// <summary>
/// 섹션 ↔ 바이트 변환. <see cref="IBinarySection"/>이면 섹션이 직접 읽고 쓰고, 아니면 JSON(Newtonsoft)이다.
///
/// 읽기는 새 인스턴스를 만들지 않고 <b>기존 인스턴스에 채운다</b>. 섹션을 받아 둔 쪽의 참조가 끊기지 않게 하기 위해서다
/// — 초기화·가져오기도 같은 인스턴스의 내용만 바꾼다.
/// </summary>
internal static class SectionCodec
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        ContractResolver = new FieldsOnlyResolver(),

        // 채울 때 기존 컬렉션에 덧붙이지 않고 갈아 끼운다.
        // 기본값(Auto)이면 필드 초기화로 들어 있던 항목 뒤에 저장된 항목이 붙어 로드할 때마다 늘어난다.
        ObjectCreationHandling = ObjectCreationHandling.Replace,

        // 저장 파일에만 있고 지금 클래스에 없는 필드(지운 필드)는 조용히 버린다.
        MissingMemberHandling = MissingMemberHandling.Ignore,

        TypeNameHandling = TypeNameHandling.None,
#if UNITY_EDITOR
        Formatting = Formatting.Indented,   // 에디터에서 파일을 열어 보기 쉽게
#else
        Formatting = Formatting.None,
#endif
    };

    public static SaveFile.Format FormatOf(SaveSection section)
        => section is IBinarySection ? SaveFile.Format.Binary : SaveFile.Format.Json;

    public static byte[] Serialize(SaveSection section)
    {
        if (section is IBinarySection binary)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
                binary.Write(writer);
            return stream.ToArray();
        }

        return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(section, section.GetType(), JsonSettings));
    }

    /// <summary>바이트를 기존 섹션 인스턴스에 채운다. 형식이 섹션과 맞지 않으면 예외를 던진다.</summary>
    public static void Populate(SaveSection section, SaveFile.Format format, byte[] payload, int version)
    {
        if (format != FormatOf(section))
            throw new InvalidDataException(
                $"저장 형식({format})이 섹션 '{section.Key}'의 현재 형식({FormatOf(section)})과 다릅니다. " +
                "JSON과 바이너리를 바꿨다면 Version을 올리고 직접 변환해야 합니다.");

        if (section is IBinarySection binary)
        {
            using var reader = new BinaryReader(new MemoryStream(payload), Encoding.UTF8);
            binary.Read(reader, version);
            return;
        }

        JsonConvert.PopulateObject(Encoding.UTF8.GetString(payload), section, JsonSettings);
    }

    /// <summary>섹션을 선언의 기본값으로 되돌린다. 인스턴스는 그대로 두고 내용만 바꾼다.</summary>
    public static void ResetToDefaults(SaveSection section)
    {
        var fresh = (SaveSection)Activator.CreateInstance(section.GetType());
        Populate(section, FormatOf(fresh), Serialize(fresh), fresh.Version);
    }

    /// <summary>
    /// public 필드만 저장한다(프로퍼티는 [JsonProperty]를 붙인 것만). JsonUtility와 같은 규칙이다.
    /// 프로퍼티까지 따라가면 Vector3.normalized 같은 계산 프로퍼티에서 자기 참조 루프 에러가 난다.
    /// </summary>
    private sealed class FieldsOnlyResolver : DefaultContractResolver
    {
        protected override List<MemberInfo> GetSerializableMembers(Type objectType)
        {
            List<MemberInfo> members = base.GetSerializableMembers(objectType);
            for (int i = members.Count - 1; i >= 0; i--)
            {
                if (members[i] is PropertyInfo property && property.GetCustomAttribute<JsonPropertyAttribute>() == null)
                    members.RemoveAt(i);
            }
            return members;
        }
    }
}

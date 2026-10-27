using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

/// <summary>
/// 화면 글의 번역. <b>원문(한국어)이 곧 키다</b> — 게임은 SO·프리팹·코드에 원문을 그대로 적고, 보일 때 <see cref="Get"/>을 거친다.
///
/// 키를 따로 짓지 않는 이유: 글이 자주 생기고 바뀌는 단계에서 키 이름 짓기·관리가 일이 되고, 키와 원문이 어긋나면
/// 화면에 엉뚱한 글이 나온다. 원문이 키면 원문을 고치는 순간 번역이 "없음"이 되어 원문이 보이고,
/// 번역표 갱신 툴이 그 줄을 새 줄로 잡는다 — 낡은 번역이 조용히 남지 않는다.
/// 같은 원문은 어디서 쓰든 같은 번역이 된다(대부분 바라는 것이고, 문맥이 달라야 하면 원문을 다르게 적는다).
///
/// 번역표는 CSV 하나(<see cref="SO_CoreConfig.LocalizationTableKey"/>, Addressables). 첫 열이 원문 언어, 나머지 열이 언어 코드,
/// '#'로 시작하는 열은 사람용 메모(어디서 쓰는지 등)라 읽지 않는다. 표가 없으면 원문만 보인다(부팅은 막지 않는다).
/// 언어 선택의 저장은 <see cref="SettingsManager"/>가 한다 — 이 매니저는 적용만 한다(볼륨과 같은 방식).
/// </summary>
public sealed class LocalizationManager : Manager
{
    private const string Tag = "[Localization]";

    private static SO_CoreConfig Config => SO_CoreConfig.Instance;

    private readonly List<string> _languages = new List<string>();
    // 언어 열 번호(원문 열 포함). 언어를 바꿀 때 _current를 다시 만든다.
    private readonly Dictionary<string, int> _columns = new Dictionary<string, int>();
    private readonly List<string[]> _rows = new List<string[]>();
    // 원문 → 지금 언어의 글. 원문 언어면 비어 있다(원문 그대로).
    private readonly Dictionary<string, string> _current = new Dictionary<string, string>();
#if UNITY_EDITOR
    // 같은 글로 매 프레임 경고가 쌓이지 않게 한 번만 알린다.
    private readonly HashSet<string> _warnedMissing = new HashSet<string>();
#endif

    public override Type[] Dependencies => new[] { typeof(ResourceManager) };

    /// <summary>원문 언어 코드. 번역표 첫 열의 이름(표가 없으면 설정 값).</summary>
    public string SourceLanguage { get; private set; }

    /// <summary>지금 보이는 언어 코드.</summary>
    public string Language { get; private set; }

    /// <summary>고를 수 있는 언어 코드들(원문 언어 포함, 표의 열 순서). 설정 화면의 언어 목록.</summary>
    public IReadOnlyList<string> Languages => _languages;

    /// <summary>언어가 바뀌었을 때. 글을 직접 채우는 표시(카드 등)가 다시 채우는 데 쓴다.</summary>
    public event Action OnLanguageChanged;

    protected override async UniTask OnInitializeAsync()
    {
        SourceLanguage = Config.SourceLanguage;
        await LoadTableAsync();
        if (_languages.Count == 0) _languages.Add(SourceLanguage);

        // 저장된 선택이 없을 때의 출발점. SettingsManager가 곧 SettingsData의 값으로 덮어쓴다.
        Language = SourceLanguage;
        SetLanguage(null);
    }

    private async UniTask LoadTableAsync()
    {
        string key = Config.LocalizationTableKey;
        if (string.IsNullOrEmpty(key)) return;

        // 표가 없는 프로젝트(번역 전)도 있다. 바로 로드하면 Addressables가 에러를 남기므로 위치부터 확인한다.
        AsyncOperationHandle<IList<IResourceLocation>> locations = Addressables.LoadResourceLocationsAsync(key, typeof(TextAsset));
        await locations.ToUniTask();
        bool exists = locations.Status == AsyncOperationStatus.Succeeded && locations.Result.Count > 0;
        Addressables.Release(locations);
        if (!exists)
        {
#if UNITY_EDITOR
            Debug.Log($"{Tag} 번역표 '{key}'가 Addressables에 없어 원문만 보입니다. Tools/Localization/Update Table로 만든다.");
#endif
            return;
        }

        TextAsset table = await Core.Resource.LoadAsync<TextAsset>(key, AssetLifetime.Persistent);
        if (table == null) return;
        ReadTable(table.text);
        // 표는 읽어서 사전으로 만들었으니 에셋은 더 들고 있을 필요가 없다.
        Core.Resource.Persistent.Release(key);
    }

    private void ReadTable(string text)
    {
        List<string[]> rows = LocalizationCsv.Parse(text);
        if (rows.Count == 0) return;

        string[] header = rows[0];
        for (int c = 0; c < header.Length; c++)
        {
            string code = header[c].Trim();
            if (code.Length == 0 || code[0] == '#') continue;
            if (_columns.ContainsKey(code)) continue;
            _columns[code] = c;
            _languages.Add(code);
        }
        if (_languages.Count == 0) return;
        SourceLanguage = _languages[0];

        int sourceColumn = _columns[SourceLanguage];
        for (int r = 1; r < rows.Count; r++)
        {
            if (rows[r].Length > sourceColumn && rows[r][sourceColumn].Length > 0) _rows.Add(rows[r]);
        }
    }

    /// <summary>
    /// 언어를 바꾼다. null·빈 값이면 기기 언어를 따른다. 표에 없는 언어면 대체 언어(<see cref="SO_CoreConfig.FallbackLanguage"/>),
    /// 그것도 없으면 원문 언어. 저장은 하지 않는다 — 설정 화면은 <see cref="SettingsManager.Language"/>를 쓴다.
    /// </summary>
    public void SetLanguage(string code)
    {
        string resolved = Resolve(string.IsNullOrEmpty(code) ? FromSystemLanguage(Application.systemLanguage) : code);
        bool changed = resolved != Language;
        Language = resolved;
        if (!changed && _current.Count > 0) return;

        _current.Clear();
        if (Language != SourceLanguage && _columns.TryGetValue(Language, out int column))
        {
            int sourceColumn = _columns[SourceLanguage];
            for (int r = 0; r < _rows.Count; r++)
            {
                string[] row = _rows[r];
                // 비어 있는 칸은 넣지 않는다 — 없으면 원문이 보인다.
                if (row.Length > column && row[column].Length > 0) _current[row[sourceColumn]] = row[column];
            }
        }
        if (changed) OnLanguageChanged?.Invoke();
    }

    private string Resolve(string code)
    {
        if (Contains(code)) return code;
        // "zh-Hans"처럼 지역이 붙은 코드는 앞부분만으로도 찾아본다.
        int dash = code.IndexOf('-');
        if (dash > 0 && Contains(code.Substring(0, dash))) return code.Substring(0, dash);
        if (Contains(Config.FallbackLanguage)) return Config.FallbackLanguage;
        return SourceLanguage;
    }

    private bool Contains(string code)
    {
        for (int i = 0; i < _languages.Count; i++)
            if (_languages[i] == code) return true;
        return false;
    }

    /// <summary>원문의 지금 언어 글. 번역이 없으면 원문 그대로.</summary>
    public string Get(string source)
    {
        if (string.IsNullOrEmpty(source) || Language == SourceLanguage) return source;
        if (_current.TryGetValue(source, out string text)) return text;
#if UNITY_EDITOR
        if (_warnedMissing.Add(source))
            Debug.LogWarning($"{Tag} '{Language}' 번역 없음: {source}\n번역표 갱신 툴(Tools/Localization/Update Table)로 표에 넣는다.");
#endif
        return source;
    }

    /// <summary>기기 언어 → 언어 코드(ISO 639-1, 중국어는 문자 체계로 나눈다). 모르는 언어는 빈 값(→ 대체 언어).</summary>
    public static string FromSystemLanguage(SystemLanguage language)
    {
        switch (language)
        {
            case SystemLanguage.Korean: return "ko";
            case SystemLanguage.English: return "en";
            case SystemLanguage.Japanese: return "ja";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified: return "zh-Hans";
            case SystemLanguage.ChineseTraditional: return "zh-Hant";
            case SystemLanguage.French: return "fr";
            case SystemLanguage.German: return "de";
            case SystemLanguage.Spanish: return "es";
            case SystemLanguage.Portuguese: return "pt";
            case SystemLanguage.Russian: return "ru";
            case SystemLanguage.Italian: return "it";
            case SystemLanguage.Vietnamese: return "vi";
            case SystemLanguage.Thai: return "th";
            case SystemLanguage.Indonesian: return "id";
            case SystemLanguage.Turkish: return "tr";
            default: return string.Empty;
        }
    }
}

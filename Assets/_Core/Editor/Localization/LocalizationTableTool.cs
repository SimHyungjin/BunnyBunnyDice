using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 번역표(CSV)를 프로젝트의 원문에 맞춘다. 원문이 곧 키라(<see cref="LocalizationManager"/>) 손으로 키를 적는 일이 없고,
/// 글을 고치거나 더한 뒤 이것만 돌리면 표가 따라온다.
///
/// 모으는 곳 세 군데:
/// - SO 에셋의 [Localized] 필드(중첩 구조체·리스트 안까지)
/// - 프리팹의 <see cref="UI_LocalizedText"/>가 붙은 글
/// - 코드의 <c>App.Localize("원문")</c> — 문자열이 호출 안에 바로 적힌 것만(변수는 찾을 수 없다)
///
/// 표에 하는 일: 새 원문은 빈 번역으로 더하고, 더는 쓰지 않는 원문은 지운다(지운 줄은 번역과 함께 로그에 남긴다).
/// 원문을 고치면 고친 원문은 새 줄이 된다 — 낡은 번역이 조용히 남지 않게.
/// 함께 검사하는 것: 번역이 빈 줄 수, 원문과 번역의 {토큰}이 다른 줄(카드 글 토큰·{0} 자리), 폰트에 없는 글자.
/// </summary>
public static class LocalizationTableTool
{
    private const string WhereColumn = "#where";
    // 한 원문이 쓰이는 곳은 몇 군데만 적는다 — 번역할 때 문맥을 보려는 것이라 전부일 필요는 없다.
    private const int MaxWhere = 3;

    private static readonly Regex CodeCall = new Regex("App\\.Localize\\(\\s*\"((?:[^\"\\\\\\n]|\\\\.)*)\"");
    private static readonly Regex Token = new Regex("\\{[^{}]*\\}");

    private sealed class Collected
    {
        public readonly List<string> Order = new List<string>();
        public readonly Dictionary<string, List<string>> Where = new Dictionary<string, List<string>>();

        public void Add(string source, string where)
        {
            if (string.IsNullOrEmpty(source)) return;
            source = source.Replace("\r\n", "\n");
            if (!Where.TryGetValue(source, out List<string> list))
            {
                list = new List<string>();
                Where[source] = list;
                Order.Add(source);
            }
            if (list.Count < MaxWhere && !list.Contains(where)) list.Add(where);
        }
    }

    // 결과는 콘솔에만 남긴다 — 대화창은 닫을 때까지 에디터를 멈춰 자동화(MCP 등)가 시간 초과로 끊긴다.
    // 표가 아직 없을 때의 저장 위치 고르기만 창을 띄운다(처음 한 번, 사람이 정할 일).
    [MenuItem("Tools/Localization/Update Table", false, 2000)]
    public static void UpdateTable()
    {
        SO_CoreConfig config = SO_CoreConfig.Instance;
        string key = config.LocalizationTableKey;
        if (string.IsNullOrEmpty(key))
        {
            UnityEngine.Debug.LogError("[Localization] SO_CoreConfig의 Localization Table Key가 비어 있습니다.");
            return;
        }

        string path = FindTablePath(key);
        if (path == null)
        {
            path = EditorUtility.SaveFilePanelInProject("번역표 만들기", key, "csv", "번역표 CSV를 둘 곳을 고른다.");
            if (string.IsNullOrEmpty(path)) return;
        }

        Collected collected = new Collected();
        List<TMP_FontAsset> fonts = new List<TMP_FontAsset>();
        try
        {
            EditorUtility.DisplayProgressBar("번역표", "SO 모으는 중", 0f);
            CollectScriptableObjects(collected);
            EditorUtility.DisplayProgressBar("번역표", "프리팹 모으는 중", 0.4f);
            CollectPrefabs(collected, fonts);
            EditorUtility.DisplayProgressBar("번역표", "코드 모으는 중", 0.8f);
            CollectCode(collected);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        StringBuilder report = new StringBuilder();
        List<string[]> rows = Merge(path, collected, config, report);
        File.WriteAllText(path, LocalizationCsv.Write(rows), new UTF8Encoding(true));
        AssetDatabase.ImportAsset(path);
        RegisterAddressable(path, key);

        if (TMP_Settings.defaultFontAsset != null && !fonts.Contains(TMP_Settings.defaultFontAsset))
            fonts.Add(TMP_Settings.defaultFontAsset);
        Check(rows, fonts, report);

        string summary = $"원문 {collected.Order.Count}줄 → {path}\n{report}";
        UnityEngine.Debug.Log($"[Localization] 번역표 갱신\n{summary}", AssetDatabase.LoadAssetAtPath<TextAsset>(path));
    }

    private static string FindTablePath(string key)
    {
        string[] guids = AssetDatabase.FindAssets(key + " t:TextAsset", new[] { "Assets" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (Path.GetFileNameWithoutExtension(path) == key && path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return path;
        }
        return null;
    }

    #region 모으기

    private static void CollectScriptableObjects(Collected collected)
    {
        Dictionary<Type, bool> hasLocalized = new Dictionary<Type, bool>();
        List<string> paths = SortedPaths("t:ScriptableObject");
        for (int i = 0; i < paths.Count; i++)
        {
            // 큰 에셋(폰트 등)까지 열지 않게 형식부터 본다.
            Type type = AssetDatabase.GetMainAssetTypeAtPath(paths[i]);
            if (type == null || !HasLocalized(type, hasLocalized, 0)) continue;
            ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(paths[i]);
            if (asset != null) Walk(asset, type, asset.name, collected, 0);
        }
    }

    private static void CollectPrefabs(Collected collected, List<TMP_FontAsset> fonts)
    {
        List<string> paths = SortedPaths("t:Prefab");
        for (int i = 0; i < paths.Count; i++)
        {
            // 모델 프리팹까지 다 열면 느리다. TMP 글이 있는 프리팹만 연다.
            string file = File.ReadAllText(paths[i]);
            if (file.IndexOf("m_fontAsset:", StringComparison.Ordinal) < 0) continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            if (prefab == null) continue;

            TMP_Text[] texts = prefab.GetComponentsInChildren<TMP_Text>(true);
            for (int t = 0; t < texts.Length; t++)
            {
                if (texts[t].font != null && !fonts.Contains(texts[t].font)) fonts.Add(texts[t].font);
                if (texts[t].GetComponent<UI_LocalizedText>() == null) continue;
                collected.Add(texts[t].text, prefab.name + "/" + PathOf(texts[t].transform, prefab.transform));
            }
        }
    }

    private static void CollectCode(Collected collected)
    {
        string[] files = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        for (int f = 0; f < files.Length; f++)
        {
            string[] lines = File.ReadAllLines(files[f]);
            for (int l = 0; l < lines.Length; l++)
            {
                if (lines[l].IndexOf("App.Localize(", StringComparison.Ordinal) < 0) continue;
                foreach (Match match in CodeCall.Matches(lines[l]))
                {
                    // 주석 속 예시(App.Localize("원문"))는 모으지 않는다.
                    string before = lines[l].Substring(0, match.Index);
                    if (before.Contains("//") || before.TrimStart().StartsWith("*", StringComparison.Ordinal)) continue;
                    collected.Add(Regex.Unescape(match.Groups[1].Value), Path.GetFileName(files[f]) + ":" + (l + 1));
                }
            }
        }
    }

    private static List<string> SortedPaths(string filter)
    {
        string[] guids = AssetDatabase.FindAssets(filter, new[] { "Assets" });
        List<string> paths = new List<string>(guids.Length);
        for (int i = 0; i < guids.Length; i++) paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
        // 표의 줄 순서가 실행마다 바뀌지 않게(비교하기 쉽게) 경로순으로.
        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    private static string PathOf(Transform target, Transform root)
    {
        string path = target.name;
        while (target.parent != null && target != root)
        {
            target = target.parent;
            if (target == root) break;
            path = target.name + "/" + path;
        }
        return path;
    }

    // 직렬화되는 필드만 본다: public(NonSerialized 아님) 또는 [SerializeField]. UnityEngine.Object 참조는 따라가지 않는다(그쪽 에셋에서 따로 모인다).
    private static IEnumerable<FieldInfo> SerializedFields(Type type)
    {
        for (Type t = type; t != null && t != typeof(ScriptableObject) && t != typeof(object); t = t.BaseType)
        {
            FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsDefined(typeof(NonSerializedAttribute), false)) continue;
                if (!field.IsPublic && !field.IsDefined(typeof(SerializeField), false)) continue;
                yield return field;
            }
        }
    }

    private static bool IsStringList(Type type) =>
        type == typeof(string[]) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) && type.GetGenericArguments()[0] == typeof(string));

    private static Type ElementType(Type type)
    {
        if (type.IsArray) return type.GetElementType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return type.GetGenericArguments()[0];
        return null;
    }

    private static bool IsNested(Type type) =>
        type != null && !type.IsPrimitive && !type.IsEnum && type != typeof(string) && !typeof(Object).IsAssignableFrom(type)
        && type.IsDefined(typeof(SerializableAttribute), false);

    private static bool HasLocalized(Type type, Dictionary<Type, bool> cache, int depth)
    {
        if (cache.TryGetValue(type, out bool has)) return has;
        cache[type] = false;   // 자기 자신을 품는 형식에서 끝나지 않는 것을 막는다.
        if (depth > 8) return false;
        foreach (FieldInfo field in SerializedFields(type))
        {
            if (field.IsDefined(typeof(LocalizedAttribute), false)) { has = true; break; }
            Type nested = ElementType(field.FieldType) ?? field.FieldType;
            if (IsNested(nested) && HasLocalized(nested, cache, depth + 1)) { has = true; break; }
        }
        cache[type] = has;
        return has;
    }

    private static void Walk(object target, Type type, string where, Collected collected, int depth)
    {
        if (target == null || depth > 8) return;
        foreach (FieldInfo field in SerializedFields(type))
        {
            Type fieldType = field.FieldType;
            object value = field.GetValue(target);
            if (value == null) continue;
            string fieldWhere = where + "." + field.Name;

            if (field.IsDefined(typeof(LocalizedAttribute), false))
            {
                if (fieldType == typeof(string)) collected.Add((string)value, fieldWhere);
                else if (IsStringList(fieldType))
                {
                    int index = 0;
                    foreach (object item in (IEnumerable)value) collected.Add(item as string, fieldWhere + "[" + index++ + "]");
                }
                continue;
            }

            Type element = ElementType(fieldType);
            if (element != null)
            {
                if (!IsNested(element)) continue;
                int index = 0;
                foreach (object item in (IEnumerable)value) Walk(item, element, fieldWhere + "[" + index++ + "]", collected, depth + 1);
            }
            else if (IsNested(fieldType)) Walk(value, fieldType, fieldWhere, collected, depth + 1);
        }
    }

    #endregion

    #region 합치기 · 검사

    private static List<string[]> Merge(string path, Collected collected, SO_CoreConfig config, StringBuilder report)
    {
        List<string[]> old = File.Exists(path) ? LocalizationCsv.Parse(File.ReadAllText(path)) : new List<string[]>();

        // 열: 원문 언어, 번역 언어들(있던 순서), #where. 표가 처음이면 원문 + 대체 언어.
        List<string> header = new List<string>();
        if (old.Count > 0)
        {
            for (int c = 0; c < old[0].Length; c++)
            {
                string name = old[0][c].Trim();
                if (name.Length > 0 && name != WhereColumn && !header.Contains(name)) header.Add(name);
            }
        }
        if (header.Count == 0) header.Add(config.SourceLanguage);
        if (!header.Contains(config.FallbackLanguage)) header.Add(config.FallbackLanguage);
        header.Add(WhereColumn);

        // 있던 번역: 원문 → (열 이름 → 글).
        Dictionary<string, Dictionary<string, string>> existing = new Dictionary<string, Dictionary<string, string>>();
        if (old.Count > 0)
        {
            string[] oldHeader = old[0];
            for (int r = 1; r < old.Count; r++)
            {
                string[] row = old[r];
                if (row.Length == 0 || row[0].Length == 0) continue;
                Dictionary<string, string> cells = new Dictionary<string, string>();
                for (int c = 1; c < row.Length && c < oldHeader.Length; c++) cells[oldHeader[c].Trim()] = row[c];
                existing[row[0]] = cells;
            }
        }

        List<string[]> rows = new List<string[]> { header.ToArray() };
        int added = 0;
        for (int i = 0; i < collected.Order.Count; i++)
        {
            string source = collected.Order[i];
            existing.TryGetValue(source, out Dictionary<string, string> cells);
            if (cells == null) added++;
            string[] row = new string[header.Count];
            row[0] = source;
            for (int c = 1; c < header.Count; c++)
            {
                if (header[c] == WhereColumn) row[c] = string.Join(" | ", collected.Where[source]);
                else row[c] = cells != null && cells.TryGetValue(header[c], out string text) ? text : string.Empty;
            }
            rows.Add(row);
        }

        report.AppendLine($"새 원문 {added}줄");
        int removed = 0;
        StringBuilder removedLog = new StringBuilder();
        foreach (KeyValuePair<string, Dictionary<string, string>> pair in existing)
        {
            if (collected.Where.ContainsKey(pair.Key)) continue;
            removed++;
            removedLog.Append("  - ").Append(Oneline(pair.Key));
            foreach (KeyValuePair<string, string> cell in pair.Value)
                if (cell.Key != WhereColumn && cell.Value.Length > 0) removedLog.Append("  [").Append(cell.Key).Append("] ").Append(Oneline(cell.Value));
            removedLog.AppendLine();
        }
        if (removed > 0) report.AppendLine($"더는 쓰지 않아 지운 줄 {removed}개:").Append(removedLog);
        return rows;
    }

    private static void Check(List<string[]> rows, List<TMP_FontAsset> fonts, StringBuilder report)
    {
        string[] header = rows[0];
        for (int c = 1; c < header.Length; c++)
        {
            if (header[c] == WhereColumn) continue;
            int empty = 0;
            for (int r = 1; r < rows.Count; r++)
                if (rows[r][c].Length == 0) empty++;
            report.AppendLine($"[{header[c]}] 번역 빈 줄 {empty}개");
        }

        // 토큰({Duration}·{0})은 번역에서도 그대로여야 한다. 빠지거나 바뀌면 숫자가 안 채워지거나 엉뚱한 값이 들어간다.
        StringBuilder mismatch = new StringBuilder();
        for (int r = 1; r < rows.Count; r++)
        {
            string tokens = Tokens(rows[r][0]);
            for (int c = 1; c < header.Length; c++)
            {
                if (header[c] == WhereColumn || rows[r][c].Length == 0) continue;
                if (Tokens(rows[r][c]) != tokens) mismatch.Append("  [").Append(header[c]).Append("] ").AppendLine(Oneline(rows[r][0]));
            }
        }
        if (mismatch.Length > 0) report.AppendLine("토큰이 원문과 다른 줄:").Append(mismatch);

        // 폰트(대체 폰트 포함)에 없는 글자는 □로 보인다. 동적 폰트는 원본 폰트 파일에 있으면 지금 아틀라스에 넣어 본다.
        for (int f = 0; f < fonts.Count; f++)
        {
            HashSet<char> missing = new HashSet<char>();
            for (int r = 1; r < rows.Count; r++)
            {
                for (int c = 0; c < header.Length; c++)
                {
                    if (header[c] == WhereColumn) continue;
                    string text = rows[r][c];
                    for (int i = 0; i < text.Length; i++)
                    {
                        char ch = text[i];
                        if (ch < ' ' || missing.Contains(ch)) continue;
                        if (!fonts[f].HasCharacter(ch, true, true)) missing.Add(ch);
                    }
                }
            }
            if (missing.Count == 0) continue;
            StringBuilder chars = new StringBuilder();
            foreach (char ch in missing) chars.Append(ch).Append("(U+").Append(((int)ch).ToString("X4")).Append(") ");
            report.AppendLine($"폰트 {fonts[f].name}에 없는 글자: {chars}");
        }
    }

    private static string Tokens(string text)
    {
        List<string> list = new List<string>();
        foreach (Match match in Token.Matches(text)) list.Add(match.Value);
        list.Sort(StringComparer.Ordinal);
        return string.Join(",", list);
    }

    private static string Oneline(string text) => text.Replace("\n", "\\n");

    #endregion

    // 런타임은 Addressables 주소로 표를 찾는다. 처음 만들 때 손으로 등록하는 일을 빼려고 여기서 맞춘다.
    private static void RegisterAddressable(string path, string key)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            UnityEngine.Debug.LogWarning($"[Localization] Addressables 설정이 없어 번역표를 등록하지 못했습니다. {path}를 주소 '{key}'로 등록한다.");
            return;
        }
        string guid = AssetDatabase.AssetPathToGUID(path);
        AddressableAssetEntry entry = settings.FindAssetEntry(guid);
        if (entry == null) entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
        if (entry.address != key) entry.SetAddress(key);
    }
}

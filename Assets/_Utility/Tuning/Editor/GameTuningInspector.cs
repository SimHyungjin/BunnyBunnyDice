using UnityEditor;
using UnityEngine;

/// <summary>
/// GameTuning 인스펙터에 검색과 원본 창구를 붙인다. 설정 SO가 늘어 펼쳐 놓고 찾기 어려워져서다.
/// - 여기 보이는 값은 복사본이라 고쳐도 이번 플레이에서만 유효하다. 설정마다 "원본 열기"로 원본 SO 에셋으로 간다(거기서 고치면 영구).
/// - 검색어가 없으면 설정마다 접고 편다.
/// - 검색어가 있으면 설정 이름이 맞는 것은 통째로, 아니면 이름·표시 이름·툴팁(한국어 설명)이 맞는 필드만 펼쳐 보인다.
///   목록·구조체는 안쪽 필드가 맞으면 그 필드를 통째로 보인다(한 칸만 떼어 그리면 어디 속한 값인지 알 수 없다).
/// - 띄어쓰기로 나눈 낱말은 모두 맞아야 한다(설정 이름 + 필드 이름을 섞어 좁힐 수 있게: "lobby tilt").
/// 검색어는 세션 동안 기억한다(플레이를 다시 해도, 오브젝트를 다시 골라도 남게).
/// </summary>
[CustomEditor(typeof(GameTuning))]
public sealed class GameTuningInspector : UnityEditor.Editor
{
    private const string QueryKey = "GameTuningInspector.Query";
    // 목록·구조체 안쪽을 볼 깊이. 설정 SO의 중첩은 얕아 이 정도면 충분하고, 깊게 돌면 매 프레임 그리기가 무거워진다.
    private const int MaxSearchDepth = 3;

    private string _query;
    private string[] _words = new string[0];

    private void OnEnable()
    {
        _query = SessionState.GetString(QueryKey, "");
        SplitWords();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawSearchField();

        SerializedProperty configs = serializedObject.FindProperty("_configs");
        int shown = 0;
        for (int i = 0; i < configs.arraySize; i++)
        {
            SerializedProperty element = configs.GetArrayElementAtIndex(i);
            Object config = element.objectReferenceValue;
            if (config == null) continue;
            if (DrawConfig(element, config)) shown++;
        }

        if (_words.Length > 0 && shown == 0) EditorGUILayout.HelpBox("맞는 설정이 없다.", MessageType.Info);
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSearchField()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        string next = EditorGUILayout.TextField(_query, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("지우기", EditorStyles.toolbarButton, GUILayout.Width(48)))
        {
            next = "";
            // 입력 중인 칸이 포커스를 쥐고 있으면 지운 값 대신 이전 글이 다시 그려진다. 지울 때만 포커스를 뺀다
            // (글자를 칠 때마다 빼면 한 글자마다 입력이 끊긴다).
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();
        if (next == _query) return;

        _query = next;
        SessionState.SetString(QueryKey, _query);
        SplitWords();
    }

    // 그렸으면 true.
    private bool DrawConfig(SerializedProperty element, Object config)
    {
        using (SerializedObject target = new SerializedObject(config))
        {
            target.Update();
            bool searching = _words.Length > 0;
            bool nameMatches = searching && MatchesAll(config.name);

            if (!searching)
            {
                element.isExpanded = DrawHeader(element.isExpanded, config, true);
                if (element.isExpanded) DrawFields(target, null);
            }
            else
            {
                // 설정 이름으로 맞으면 전부, 아니면 낱말마다 설정 이름이나 필드 어느 쪽에든 맞는 필드만.
                if (!nameMatches && !HasMatchingField(target, config.name)) return false;
                DrawHeader(true, config, false);
                DrawFields(target, nameMatches ? null : config.name);
            }
            target.ApplyModifiedProperties();
            return true;
        }
    }

    // 설정 이름 줄 + 원본 열기. 펼쳤는지를 돌려준다(foldout이 아니면 그대로).
    private bool DrawHeader(bool expanded, Object config, bool foldout)
    {
        EditorGUILayout.BeginHorizontal();
        if (foldout) expanded = EditorGUILayout.Foldout(expanded, config.name, true);
        else EditorGUILayout.LabelField(config.name, EditorStyles.boldLabel);

        SO_Config original = ((GameTuning)target).GetOriginal(config as SO_Config);
        using (new EditorGUI.DisabledScope(original == null))
        {
            // 원본 SO 에셋을 골라 인스펙터에 띄운다 — 거기서 고친 값은 영구 저장되고, 이 복사본도 원본 값으로 다시 덮인다.
            if (GUILayout.Button("원본 열기", EditorStyles.miniButton, GUILayout.Width(64)))
            {
                Selection.activeObject = original;
                EditorGUIUtility.PingObject(original);
            }
        }
        EditorGUILayout.EndHorizontal();
        return expanded;
    }

    // configName이 null이면 모든 필드를, 아니면 맞는 필드만 그린다.
    private void DrawFields(SerializedObject target, string configName)
    {
        EditorGUI.indentLevel++;
        SerializedProperty it = target.GetIterator();
        bool enterChildren = true;
        while (it.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (it.propertyPath == "m_Script") continue;
            if (configName != null && !FieldMatches(it, configName)) continue;
            EditorGUILayout.PropertyField(it, true);
        }
        EditorGUI.indentLevel--;
    }

    private bool HasMatchingField(SerializedObject target, string configName)
    {
        SerializedProperty it = target.GetIterator();
        bool enterChildren = true;
        while (it.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (it.propertyPath == "m_Script") continue;
            if (FieldMatches(it, configName)) return true;
        }
        return false;
    }

    // 낱말마다 설정 이름 또는 이 필드(안쪽 포함)의 글에 들어 있어야 한다.
    private bool FieldMatches(SerializedProperty property, string configName)
    {
        for (int i = 0; i < _words.Length; i++)
        {
            if (Contains(configName, _words[i])) continue;
            if (!PropertyContains(property, _words[i], 0)) return false;
        }
        return true;
    }

    private static bool PropertyContains(SerializedProperty property, string word, int depth)
    {
        if (Contains(property.name, word) || Contains(property.displayName, word) || Contains(property.tooltip, word)) return true;
        if (depth >= MaxSearchDepth || !property.hasVisibleChildren) return false;

        SerializedProperty child = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        if (!child.NextVisible(true)) return false;
        while (!SerializedProperty.EqualContents(child, end))
        {
            if (PropertyContains(child, word, depth + 1)) return true;
            if (!child.NextVisible(false)) break;
        }
        return false;
    }

    private bool MatchesAll(string text)
    {
        for (int i = 0; i < _words.Length; i++)
            if (!Contains(text, _words[i])) return false;
        return true;
    }

    private static bool Contains(string text, string word) =>
        !string.IsNullOrEmpty(text) && text.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0;

    private void SplitWords()
    {
        string[] parts = (_query ?? "").Split(' ');
        int count = 0;
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0) count++;
        _words = new string[count];
        count = 0;
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0) _words[count++] = parts[i];
    }
}

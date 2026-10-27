using UnityEditor;
using UnityEngine;

/// <summary>
/// [InlineScriptableObject] 필드: 참조 필드 아래에 대상 SO의 필드를 접고 펼 수 있게 그린다.
/// 고친 값은 대상 SO의 SerializedObject로 적용하므로 Undo가 되고, 대상의 OnValidate도 불린다.
/// </summary>
[CustomPropertyDrawer(typeof(InlineScriptableObjectAttribute))]
public sealed class PropertyDrawer_InlineScriptableObject : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded || property.objectReferenceValue == null) return height;

        using (SerializedObject target = new SerializedObject(property.objectReferenceValue))
        {
            SerializedProperty it = target.GetIterator();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (it.propertyPath == "m_Script") continue;
                height += EditorGUI.GetPropertyHeight(it, true) + EditorGUIUtility.standardVerticalSpacing;
            }
        }
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Object value = property.objectReferenceValue;
        // 리스트 요소는 "Element 0" 대신 대상 이름을 보여준다.
        if (value != null && property.propertyPath.EndsWith("]")) label = new GUIContent(value.name);

        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(line, property, label);
        if (value == null) return;

        Rect foldout = new Rect(line.x, line.y, EditorGUIUtility.labelWidth, line.height);
        property.isExpanded = EditorGUI.Foldout(foldout, property.isExpanded, GUIContent.none, true);
        if (!property.isExpanded) return;

        using (SerializedObject target = new SerializedObject(value))
        {
            target.Update();
            EditorGUI.indentLevel++;
            float y = line.yMax + EditorGUIUtility.standardVerticalSpacing;
            SerializedProperty it = target.GetIterator();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (it.propertyPath == "m_Script") continue;
                float h = EditorGUI.GetPropertyHeight(it, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), it, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
            }
            EditorGUI.indentLevel--;
            target.ApplyModifiedProperties();
        }
    }
}

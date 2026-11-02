using UnityEditor;
using UnityEngine;

/// <summary>
/// SO_SoundCue 필드 옆에 ▶ 버튼을 붙인다 — 큐를 모은 SO·카드 SO 등 큐를 넣는 칸 어디서든 에셋을 열지 않고 바로 들어 보게.
/// </summary>
[CustomPropertyDrawer(typeof(SO_SoundCue))]
public sealed class PropertyDrawer_SoundCue : PropertyDrawer
{
    private const float ButtonWidth = 24f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Rect field = new Rect(position.x, position.y, position.width - ButtonWidth - 2f, position.height);
        Rect button = new Rect(position.xMax - ButtonWidth, position.y, ButtonWidth, position.height);
        EditorGUI.PropertyField(field, property, label);
        using (new EditorGUI.DisabledScope(property.objectReferenceValue == null))
        {
            if (SoundPreview.Button(button)) SoundPreview.Play(property.objectReferenceValue as SO_SoundCue);
        }
    }
}

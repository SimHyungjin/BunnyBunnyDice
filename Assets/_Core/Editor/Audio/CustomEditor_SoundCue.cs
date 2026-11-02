using UnityEditor;
using UnityEngine;

/// <summary>
/// 큐 인스펙터 위쪽에 미리 듣기를 둔다: 큐 그대로(무작위 클립·피치) 한 번, 클립마다 하나씩.
/// 클립을 고르거나 볼륨·피치를 맞출 때 매번 오디오 파일을 찾아 누르지 않게.
/// </summary>
[CustomEditor(typeof(SO_SoundCue))]
public sealed class CustomEditor_SoundCue : Editor
{
    public override void OnInspectorGUI()
    {
        SO_SoundCue cue = (SO_SoundCue)target;

        EditorGUILayout.LabelField("미리 듣기", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!cue.HasClips))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 무작위 재생(볼륨·피치 적용)")) SoundPreview.Play(cue);
                if (GUILayout.Button("■", GUILayout.Width(28f))) SoundPreview.Stop();
            }
        }
        if (cue.Clips != null)
        {
            for (int i = 0; i < cue.Clips.Length; i++)
            {
                AudioClip clip = cue.Clips[i];
                if (clip == null) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    // 클립 하나를 고를 때는 피치를 흔들지 않는다 — 클립끼리 비교가 되게.
                    if (GUILayout.Button("▶", EditorStyles.miniButton, GUILayout.Width(28f))) SoundPreview.Play(clip, cue.Volume);
                    EditorGUILayout.LabelField(clip.name, $"{clip.length:0.00}s");
                }
            }
        }
        EditorGUILayout.Space();

        DrawDefaultInspector();
    }
}

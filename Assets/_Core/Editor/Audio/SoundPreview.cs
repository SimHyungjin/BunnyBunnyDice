using UnityEditor;
using UnityEngine;

/// <summary>
/// 에디터에서 효과음을 미리 듣는다(인스펙터의 ▶ 버튼). 숨긴 오브젝트의 AudioSource 하나로 낸다 — 플레이 중이 아니어도 들리고,
/// 큐의 볼륨·피치 범위를 그대로 따라 게임에서 날 소리와 같게 들린다(믹서·채널 볼륨은 거치지 않는다).
/// 새로 누르면 앞 소리를 끊는다 — 여러 개를 연달아 비교할 때 겹치지 않게.
/// </summary>
public static class SoundPreview
{
    private static AudioSource _source;
    private static AudioClip _last;

    public static void Play(SO_SoundCue cue)
    {
        if (cue == null || !cue.HasClips) return;
        // 큐 재생과 같게 직전 클립은 피해 무작위로 고른다(클립이 여럿일 때).
        AudioClip clip = null;
        for (int tries = 0; tries < 8; tries++)
        {
            AudioClip pick = cue.Clips[Random.Range(0, cue.Clips.Length)];
            if (pick == null) continue;
            clip = pick;
            if (pick != _last) break;
        }
        Play(clip, cue.Volume, Random.Range(cue.PitchRange.x, cue.PitchRange.y));
    }

    public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        AudioSource source = GetSource();
        source.Stop();
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.Play();
        _last = clip;
    }

    public static void Stop()
    {
        if (_source != null) _source.Stop();
    }

    private static AudioSource GetSource()
    {
        if (_source != null) return _source;
        // 씬에 저장되지 않고 하이어라키에도 보이지 않게. 도메인 리로드·씬 전환에 사라지면 다시 만든다.
        GameObject go = new GameObject("SoundPreview") { hideFlags = HideFlags.HideAndDontSave };
        _source = go.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        return _source;
    }

    /// <summary>한 줄 높이의 ▶ 버튼. 눌렸으면 true.</summary>
    public static bool Button(Rect rect, string tooltip = "미리 듣기")
    {
        return GUI.Button(rect, new GUIContent("▶", tooltip), EditorStyles.miniButton);
    }
}

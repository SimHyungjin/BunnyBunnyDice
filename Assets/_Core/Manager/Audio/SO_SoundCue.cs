using UnityEngine;

/// <summary>
/// 효과음 하나의 정의. 소리 하나가 에셋 하나다 — 중앙 표·enum·코드 생성이 없다.
///
/// 쓰는 쪽은 에셋을 직접 참조한다.
///  - 오브젝트에 딸린 소리: 컴포넌트에 <c>[SerializeField] SO_SoundCue</c>를 두고 끌어다 놓는다.
///  - 코드가 이름으로 부르는 소리(UI 클릭 등): 게임 쪽에 큐를 필드로 모은 SO를 하나 두고 그 필드로 부른다.
///    필드라 오타는 컴파일 에러가 되고, 순서와 무관하다.
///    <code>
///    public sealed class SO_UISounds : ScriptableObject { public SO_SoundCue Click; public SO_SoundCue Confirm; }
///    App.PlaySfx(uiSounds.Click);
///    </code>
///    그 묶음 SO를 로드하면 담긴 클립도 함께 메모리에 올라가므로, 작고 늘 쓰는 소리만 담는다.
/// </summary>
[CreateAssetMenu(fileName = "SoundCue", menuName = "Core/Audio/Sound Cue")]
public sealed class SO_SoundCue : ScriptableObject
{
    [Tooltip("여러 개면 재생할 때마다 무작위로 고른다(직전과 같은 것은 피한다).")]
    public AudioClip[] Clips = new AudioClip[0];

    [Tooltip("볼륨 설정에서 어느 채널을 따를지.")]
    public AudioChannelType Channel = AudioChannelType.Sound;

    [Range(0f, 1f)] public float Volume = 1f;

    [Tooltip("재생마다 이 범위에서 무작위 피치. 같은 값이면 고정.")]
    public Vector2 PitchRange = Vector2.one;

    [Tooltip("소스가 모자랄 때 빼앗기는 순서. 숫자가 클수록 덜 중요하다(Unity AudioSource.priority와 같다).")]
    [Range(0, 256)] public int Priority = 128;

    [Tooltip("끝없이 반복한다(엔진음 등). 돌려받은 핸들로 멈춘다.")]
    public bool Loop;

    [Header("3D (위치를 넘겨 재생할 때만)")]
    [Range(0f, 1f)] public float SpatialBlend = 1f;
    public float MinDistance = 1f;
    public float MaxDistance = 500f;
    public AudioRolloffMode RolloffMode = AudioRolloffMode.Logarithmic;

    [Header("제한")]
    [Tooltip("이 큐가 동시에 낼 수 있는 최대 개수. 넘으면 가장 오래된 것을 끊고 낸다. 0이면 제한 없음.")]
    [Min(0)] public int MaxInstances;

    [Tooltip("같은 큐를 다시 낼 수 있는 최소 간격(초, 실제 시간). 한 프레임에 코인 20개를 먹는 식의 겹침을 막는다. 0이면 제한 없음.")]
    [Min(0f)] public float MinInterval;

    public bool HasClips
    {
        get
        {
            if (Clips == null) return false;
            for (int i = 0; i < Clips.Length; i++)
                if (Clips[i] != null) return true;
            return false;
        }
    }

    private void OnValidate()
    {
        // 큐는 Music·Sound 채널에만 속한다. Master는 둘 다를 덮는 볼륨이지 소리가 나가는 곳이 아니다.
        if (Channel == AudioChannelType.Master) Channel = AudioChannelType.Sound;
        if (PitchRange.y < PitchRange.x) PitchRange.y = PitchRange.x;
    }
}

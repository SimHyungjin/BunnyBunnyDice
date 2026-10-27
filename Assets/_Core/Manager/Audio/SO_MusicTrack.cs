using UnityEngine;

/// <summary>
/// 배경음 하나의 정의. 인트로와 본체를 나눠 두면 인트로를 한 번 재생한 뒤 본체를 샘플 단위로 정확히 이어 붙여 반복한다.
///
/// 클립 하나 안에서 루프 지점을 매 프레임 확인해 되감는 방식은 프레임 단위라 이음새에서 클릭음·끊김이 생긴다.
/// 대신 루프 구간만 잘라 Body로, 그 앞을 Intro로 두면 이음새가 정확하다.
/// </summary>
[CreateAssetMenu(fileName = "MusicTrack", menuName = "Core/Audio/Music Track")]
public sealed class SO_MusicTrack : ScriptableObject
{
    [Tooltip("처음 한 번만 나오는 앞부분. 없으면 비운다.")]
    public AudioClip Intro;

    [Tooltip("본체. Loop가 켜져 있으면 이것만 반복한다.")]
    public AudioClip Body;

    public bool Loop = true;

    [Range(0f, 1f)] public float Volume = 1f;

    public bool HasClip => Intro != null || Body != null;
}

/// <summary>
/// 재생 중인 효과음 하나를 가리키는 핸들. 루프 효과음을 멈추거나 끝났는지 확인할 때 쓴다.
/// 소스가 다른 소리에 재사용되면 핸들은 무효가 된다(세대 번호로 판정) — 남의 소리를 멈추는 일은 없다.
/// 기본값(default)은 아무것도 가리키지 않는다.
/// </summary>
public readonly struct SoundHandle
{
    internal readonly int Voice;
    internal readonly int Generation;

    internal SoundHandle(int voice, int generation)
    {
        Voice = voice;
        Generation = generation;
    }

    /// <summary>아직 그 소리가 나고 있는지.</summary>
    public bool IsPlaying => Core.Audio != null && Core.Audio.IsPlaying(this);

    /// <summary>그 소리를 멈춘다. 이미 끝났거나 재사용됐으면 아무것도 하지 않는다.</summary>
    public void Stop() => Core.Audio?.StopSfx(this);
}

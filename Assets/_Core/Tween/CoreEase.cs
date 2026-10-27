/// <summary>
/// 트윈 가감속 곡선.
///
/// 이름을 <c>Ease</c>가 아니라 <c>CoreEase</c>로 둔 이유: 게임 코드가 나중에 PrimeTween 같은
/// 외부 트윈 라이브러리를 가져와도 전역 이름이 충돌하지 않게 하려는 것이다.
/// 코어는 외부 트윈에 의존하지 않지만, 게임 쪽에서 쓰는 것까지 막을 이유는 없다.
/// </summary>
public enum CoreEase
{
    Linear,
    InQuad,  OutQuad,  InOutQuad,
    InCubic, OutCubic, InOutCubic,
    InBack,  OutBack,
}

public static class CoreEaseExtensions
{
    /// <summary>표준 back easing이 쓰는 되돌아가는 정도.</summary>
    private const float BACK_S = 1.70158f;

    /// <summary>
    /// 정규화된 진행도 t(0~1)를 곡선에 통과시킨다.
    /// InBack/OutBack은 0~1 밖으로 넘어가므로(의도된 오버슈트),
    /// 결과를 쓰는 쪽은 클램프가 필요한 값(알파·볼륨)만 따로 잘라야 한다.
    /// </summary>
    public static float Evaluate(this CoreEase ease, float t)
    {
        switch (ease)
        {
            case CoreEase.InQuad:  return t * t;
            case CoreEase.OutQuad: return 1f - (1f - t) * (1f - t);
            case CoreEase.InOutQuad:
                return t < 0.5f
                    ? 2f * t * t
                    : 1f - (-2f * t + 2f) * (-2f * t + 2f) * 0.5f;

            case CoreEase.InCubic:  return t * t * t;
            case CoreEase.OutCubic: return 1f - Cube(1f - t);
            case CoreEase.InOutCubic:
                return t < 0.5f
                    ? 4f * t * t * t
                    : 1f - Cube(-2f * t + 2f) * 0.5f;

            case CoreEase.InBack:  return t * t * ((BACK_S + 1f) * t - BACK_S);
            case CoreEase.OutBack:
            {
                float p = t - 1f;
                return 1f + p * p * ((BACK_S + 1f) * p + BACK_S);
            }

            default: return t;
        }
    }

    private static float Cube(float v) => v * v * v;
}

using UnityEngine;

/// <summary>
/// 쾌락주의자: 타워 주사위를 굴릴 때 가운데 눈이 나오지 않는다(기획서 10.3). 평균은 그대로지만 흔들림이 커진다
/// — 합성 상한에서 넘친 눈이 버려지고 눈 20에 각성이 붙으므로 실제 성능은 같지 않다. 그 차이가 이 캐릭터의 재미다.
/// 빼는 눈 수는 짝수로 맞춰 가운데를 대칭으로 비운다(d6이면 3·4, d20이면 8~13).
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/No Middle", fileName = "SO_HeroTrait_NoMiddle")]
public sealed class SO_HeroTrait_NoMiddle : SO_HeroTrait, IDicePipRoller
{
    [Tooltip("빼는 가운데 눈의 비율(면 수 대비). 1/3이면 d6에서 2개, d20에서 6개.")]
    [Range(0f, 0.9f)] public float MiddleExcludeRatio = 0.333f;

    public int RollPips(int faces)
    {
        // 짝수 개를 빼야 양쪽에 같은 수가 남는다. 적어도 양 끝 하나씩은 남긴다.
        int excluded = 2 * Mathf.RoundToInt(faces * MiddleExcludeRatio * 0.5f);
        excluded = Mathf.Clamp(excluded, 0, Mathf.Max(faces - 2, 0));
        int remaining = faces - excluded;
        int low = remaining / 2;
        int pick = Random.Range(0, remaining);
        return pick < low ? pick + 1 : faces - (remaining - 1 - pick);
    }
}

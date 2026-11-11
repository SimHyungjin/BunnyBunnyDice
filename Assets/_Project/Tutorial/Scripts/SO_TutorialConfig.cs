using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 튜토리얼(상황 팁) 설정: 팁 글·비출 곳과 어두운 판 연출 값.
/// 팁은 처음 그 상황이 오면 한 번만 뜬다 — 판을 멈추고, 설명할 곳만 남기고 화면을 어둡게 덮는다.
/// 해 보면 알 수 있는 것(색별 공격 방식·상성 등)은 넣지 않고, 화면만 봐서는 알 수 없는 규칙만 둔다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Game/Tutorial Config", fileName = nameof(SO_TutorialConfig))]
public sealed class SO_TutorialConfig : SO_Config
{
    [Tooltip("팁 목록. 같은 Id가 둘이면 앞의 것만 쓴다.")]
    public List<TutorialTip> Tips = new List<TutorialTip>();

    [Header("어두운 판")]
    [Tooltip("덮개 색(알파 = 어둡기).")]
    public Color DimColor = new Color(0f, 0f, 0f, 0.72f);
    [Tooltip("덮개가 나타나고 사라지는 시간(초, 실제 시간). 장을 넘길 때 구멍은 새 자리로 미끄러진다.")]
    [Min(0f)] public float FadeDuration = 0.2f;
    [Tooltip("구멍이 새 자리로 따라가는 빠르기(클수록 빨리 붙는다). 대상이 움직여도(손패 부채꼴·영웅) 구멍이 따라간다.")]
    [Min(0f)] public float HoleFollowSharpness = 14f;
    [Tooltip("구멍 둘레 여백 — 화면 짧은 변 대비 비율. 대상에 딱 붙으면 테두리가 잘려 보인다.")]
    [Min(0f)] public float HolePadding = 0.012f;
    [Tooltip("구멍의 최소 크기(가로·세로) — 화면 짧은 변 대비 비율. 멀리 작게 보이는 대상(영웅)도 구멍이 눈에 띄게.")]
    [Min(0f)] public float MinHoleSize = 0.14f;
    [Tooltip("구멍 모서리 둥글기 — 구멍 짧은 변의 절반 대비 비율(1이면 짧은 쪽이 반원).")]
    [Range(0f, 1f)] public float HoleCorner = 0.5f;
    [Tooltip("구멍 가장자리 흐림 — 화면 짧은 변 대비 비율.")]
    [Min(0f)] public float HoleSoftness = 0.01f;
    [Tooltip("영웅 구멍의 너비 = 키 × 이 값.")]
    [Min(0f)] public float HeroHoleAspect = 0.7f;

    [Header("주사위 강조 (TutorialPage.PulseDice)")]
    [Tooltip("비춘 주사위가 얹기 연출로 튀는 간격(초, 실제 시간). 장이 뜨면 바로 한 번 튄다.")]
    [Min(0.1f)] public float DicePulseInterval = 1.2f;

    [Header("설명 상자")]
    [Tooltip("상자는 구멍 반대쪽(위·아래)에 붙는다. 한 장 안에서 구멍 가운데가 화면 가운데를 이만큼(화면 높이 비율) 넘어야 반대쪽으로 옮긴다.")]
    [Range(0f, 0.5f)] public float BoxSwitchMargin = 0.1f;

    [Header("넘기기")]
    [Tooltip("장이 바뀐 뒤 누름을 받기까지(초) — 팁을 띄운 누름·연타로 읽기도 전에 넘어가지 않게.")]
    [Min(0f)] public float InputLockDuration = 0.4f;

    /// <summary>그 Id의 팁. 없으면 null.</summary>
    public TutorialTip Find(TutorialTipId id)
    {
        for (int i = 0; i < Tips.Count; i++)
        {
            if (Tips[i] != null && Tips[i].Id == id) return Tips[i];
        }
        return null;
    }
}

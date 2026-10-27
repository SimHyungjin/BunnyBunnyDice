using UnityEngine;

/// <summary>
/// 판 밖 주사위 굴림 연출(리롤·판 보상·캐릭터 열기의 첫 능력치) 설정. 셋이 같은 굴림(UI_MetaDie)을 쓰므로 박자도 한 곳에서 맞춘다.
/// 시간은 모두 실제 시간(초)이다 — 판 밖 화면이라 게임 시간 배율과 상관없다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Meta/Meta Dice Config", fileName = nameof(SO_MetaDiceConfig))]
public sealed class SO_MetaDiceConfig : SO_Config
{
    [Header("구르기 (빈 주사위가 빙글 돌다 감속 — 눈은 착지 때 나온다)")]
    [Tooltip("누른 뒤 착지까지(감속 포함). 구르는 동안 다시 누르면(리롤 연타) 이 시간이 처음부터 다시 흐른다 — 손을 멈춰야 착지한다.")]
    [Min(0.05f)] public float TumbleDuration = 0.7f;
    [Tooltip("끝의 감속 구간. 이 동안 서서히 느려지며 바로 선 자세(180° 단위 — 눈 모양이 같다)로 멈춘다.")]
    [Min(0.05f)] public float DecelDuration = 0.45f;
    [Tooltip("최고 회전 속도(초당 각도).")]
    [Min(0f)] public float SpinSpeed = 1080f;
    [Tooltip("도는 동안 커지는 비율(감속하며 원래 크기로).")]
    [Range(0f, 0.5f)] public float TumbleBounce = 0.1f;

    [Header("착지")]
    [Tooltip("착지 순간 톡 커지는 크기.")]
    [Range(1f, 2f)] public float LandPopScale = 1.3f;
    [Min(0.01f)] public float LandDuration = 0.18f;

    [Header("올랐을 때")]
    [Tooltip("주사위 몸이 이 색으로 반짝였다가 원래 색으로 돌아온다.")]
    public Color SuccessColor = new Color(1f, 0.88f, 0.29f);
    [Min(0.01f)] public float SuccessDuration = 0.35f;

    [Header("그대로일 때")]
    [Tooltip("굴린 눈을 흐리게 보이는 시간. 그 뒤 원래 눈으로 돌아오며 고개를 젓는다.")]
    [Min(0f)] public float FailHold = 0.3f;
    [Tooltip("굴린 눈의 투명도.")]
    [Range(0f, 1f)] public float FailAlpha = 0.35f;
    [Tooltip("고개 젓기 각도.")]
    [Range(0f, 45f)] public float FailShakeAngle = 12f;
    [Min(0.01f)] public float FailShakeDuration = 0.3f;

    [Header("여러 개 · 보상 · 열기")]
    [Tooltip("캐릭터를 열 때 능력치 6칸이 하나씩 굴러 나오는 간격.")]
    [Min(0f)] public float Stagger = 0.1f;
    [Tooltip("결과 화면이 뜬 뒤 보상 주사위를 굴리기까지(창이 열리는 연출을 기다린다).")]
    [Min(0f)] public float RewardDelay = 0.35f;
    [Tooltip("보상 숫자가 0부터 오르는 시간.")]
    [Min(0.01f)] public float CountUpDuration = 0.4f;
    [Tooltip("보상 주사위가 다 착지한 뒤 기다렸다가(GatherDelay) 합 숫자 자리로 모이며 사라지는 시간.")]
    [Min(0.01f)] public float GatherDuration = 0.3f;
    [Min(0f)] public float GatherDelay = 0.35f;
    [Tooltip("선택 창에서 실루엣이 걷히는 시간. 끝나면 창이 닫히고 로비에서 첫 능력치를 굴린다.")]
    [Min(0.01f)] public float RevealDuration = 0.5f;
    [Tooltip("실루엣이 걷힐 때 칸이 커지는 크기.")]
    [Range(1f, 1.5f)] public float RevealPopScale = 1.08f;
}

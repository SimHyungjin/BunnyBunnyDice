using UnityEngine;

/// <summary>
/// 카드 한 종류(SO 하나)의 공통 내용: 카드에 적히는 글과 아이콘, 레벨업 출현 가중치.
/// 주사위 카드는 색 × 면체 값이라 SO가 아니고(DiceCard), 칩 카드(SO_ChipCard)·스킬 카드(SO_SkillCard)가 이것을 잇는다.
/// 카드 종류는 "어디에 끌어다 놓는가"로 나눈다 — 종류만 보고 놓을 곳을 알 수 있게(기획서 5장).
/// 레벨업 설정(SO_LevelUpConfig)의 후보 목록이 참조하므로 의존성으로 함께 로드된다.
/// </summary>
public abstract class SO_Card : SO_Config
{
    [Header("카드")]
    [Tooltip("카드 제목.")]
    [Localized] public string DisplayName;
    [Tooltip("한두 줄 설명. 카드를 누르고 있으면 읽힌다.")]
    [Localized, TextArea] public string Description;
    [Tooltip("강한 상대(몬스터 특성, 기획서 9.1). 비우면 표시하지 않는다.")]
    [Localized] public string StrongAgainst;
    [Tooltip("카드 아이콘. 흰 그림이면 IconColor를 곱해 쓰고(TintIcon), 색칠된 그림이면 그대로 쓴다.")]
    public Sprite Icon;
    [Tooltip("아이콘에 IconColor를 곱할지. 흰 그림 하나를 색만 바꿔 돌려 쓰는 카드(칩)는 켜고, 색칠된 그림은 끈다 — 끄면 그림이 물들지 않는다.")]
    public bool TintIcon = true;
    [Tooltip("카드의 색 — 아이콘(TintIcon일 때)과 연출(스킬 고리·영웅 외곽선·판 위 칩 색)에 쓴다.")]
    public Color IconColor = Color.white;

    [Header("출현")]
    [Tooltip("레벨업 출현 가중치(희귀도). 주사위 카드 한 종류가 1이다. 테스트 동안은 모두 1.")]
    [Min(0f)] public float Weight = 1f;
}

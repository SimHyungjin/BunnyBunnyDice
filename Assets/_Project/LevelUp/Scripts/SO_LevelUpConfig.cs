using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경험치·레벨업 카드 설정(잠정). 몬스터를 잡아 경험치가 차면 레벨이 오르고 카드 몇 장 중 하나를 고른다(기획서 5장).
/// 주사위·칩·스킬·영웅 강화 카드가 섞여 나온다. 주사위 카드는 색과 면체를 따로 뽑아 만들고(9.10 — 색 × 면체 조합을 하나씩 정하지 않게),
/// 칩·스킬 카드는 종류마다의 가중치(희귀도)로 뽑는다. 주사위 카드 한 종류(색 하나 × 면체 하나)의 몫이 색 가중치 × 면체 가중치라,
/// 모두 1이면 50종(주사위 24 + 칩 7 + 스킬 14 + 영웅 강화 5)이 같은 확률로 나온다(5.2 — 테스트 동안은 주사위 카드 보장 없이 모든 가중치 1).
/// </summary>
[CreateAssetMenu(menuName = "Def/LevelUp/Level Up Config", fileName = nameof(SO_LevelUpConfig))]
public sealed class SO_LevelUpConfig : SO_Config
{
    [Serializable]
    public struct ColorWeight
    {
        public DiceColor Colors;
        [Min(0f)] public float Weight;
    }

    [Serializable]
    public struct FacesWeight
    {
        public int Faces;
        [Min(0f)] public float Weight;
    }

    [Header("경험치")]
    [Tooltip("1레벨에서 2레벨로 오르는 데 드는 경험치.")]
    [Min(1)] public int BaseExp = 5;
    [Tooltip("레벨마다 늘어나는 필요 경험치.")]
    [Min(0)] public int ExpPerLevel = 3;

    [Header("선택")]
    [Tooltip("한 번에 보여 주는 카드 수. 후보가 모자라면 있는 만큼만.")]
    [Min(1)] public int ChoiceCount = 3;
    [Tooltip("고르는 동안의 게임 속도 배율(기획: 50%). 카드를 끄는 감속과 겹치면 더 느린 쪽을 쓴다.")]
    [Range(0.05f, 1f)] public float ChoiceTimeScale = 0.5f;
    [Tooltip("레벨업마다 캐릭터 전용 카드(SO_HeroCharacter.ExclusiveCards 중 한 장)가 후보 한 자리에 나올 확률. 나오면 나머지 자리만 공용 카드로 뽑는다 " +
             "— 다 뽑은 뒤 덮어쓰면 레인보우의 '종류가 모두 다름'이 깨지고 고르려던 카드가 사라졌다(2026-10-29 사용자).")]
    [Range(0f, 1f)] public float ExclusiveChance = 0.2f;
    [Tooltip("x2 후보 카드(특성 — 더블)가 팝업이 열릴 때 이 배율에서 원래 크기로 톡 줄어든다 — 붙은 것이 눈에 띄게.")]
    [Min(1f)] public float CopyPopScale = 1.15f;
    [Tooltip("팝업이 뜬 뒤 이 시간(초, 실제 시간) 동안은 카드를 눌러도 고르지 않는다 — 맵·손패를 누르던 손가락이 갑자기 뜬 카드를 잘못 고르지 않게.")]
    [Min(0f)] public float InputLockDuration = 0.3f;
    [Tooltip("카드 밖을 누르고 있으면 제목·카드가 이 시간(초, 실제 시간)에 걸쳐 사라지고 떼면 다시 나타난다 — 고르기 전에 판을 보게(검정 가드는 그대로).")]
    [Min(0.01f)] public float PeekFadeDuration = 0.15f;
    [Tooltip("색 상태별 출현 가중치(잠정). 8색이 같은 가중치로 나온다 — 단색이 받는 카드가 8종 중 2종이라는 등급의 근거(기획서 6.5·9.6). 0이거나 빠진 색은 나오지 않는다.")]
    public List<ColorWeight> ColorWeights = new List<ColorWeight>();
    [Tooltip("면체별 출현 가중치(잠정). 테스트 동안은 모두 1 — d20 희귀도는 최종 조정 때 정한다(13).")]
    public List<FacesWeight> FacesWeights = new List<FacesWeight>();
    [Tooltip("나오는 스킬 카드(주사위 대상 — 페인트·다시 굴리기·잭팟·레이즈, 범위·판 전체 — 기획서 8). 가중치는 카드마다(Weight). 빠진 카드는 나오지 않는다. "
        + "캐릭터 전용 카드는 여기 넣지 않는다 — 넣으면 모두에게 나온다(SO_HeroCharacter.ExclusiveCards).")]
    [UnityEngine.Serialization.FormerlySerializedAs("UpgradeCards")]
    public List<SO_SkillCard> SkillCards = new List<SO_SkillCard>();
    [Tooltip("나오는 칩 카드(7색, 기획서 7). 가중치는 카드마다(Weight). 판 시작 칩도 이 목록·가중치로 뽑는다(ChipField).")]
    public List<SO_ChipCard> ChipCards = new List<SO_ChipCard>();
    [Tooltip("나오는 영웅 강화 카드(기획서 10.4). 고르는 즉시 적용되고 손패에 들어가지 않는다. 가중치는 카드마다(Weight).")]
    public List<SO_HeroCard> HeroCards = new List<SO_HeroCard>();

    /// <summary>level에서 다음 레벨로 오르는 데 드는 경험치.</summary>
    public int GetExpToNext(int level) => BaseExp + ExpPerLevel * (level - 1);

    public override void SwapNested(Func<SO_Config, SO_Config> swap)
    {
        for (int i = 0; i < SkillCards.Count; i++) SkillCards[i] = (SO_SkillCard)swap(SkillCards[i]);
        for (int i = 0; i < ChipCards.Count; i++) ChipCards[i] = (SO_ChipCard)swap(ChipCards[i]);
        for (int i = 0; i < HeroCards.Count; i++) HeroCards[i] = (SO_HeroCard)swap(HeroCards[i]);
    }
}

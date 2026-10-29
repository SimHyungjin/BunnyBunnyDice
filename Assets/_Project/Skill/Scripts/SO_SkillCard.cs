using UnityEngine;

/// <summary>스킬을 어디에 쓰는가. 카드를 끌어 놓을 곳이 이것으로 정해진다 — 한 번의 드래그로 발동하므로 대상은 늘 하나다(기획서 8).</summary>
public enum SkillTarget
{
    /// <summary>놓인 주사위 하나(페인트·다시 굴리기·잭팟·레이즈).</summary>
    Dice,
    /// <summary>판 위의 모든 적(포켓·랙·백스핀·체크메이트·체크). 판 위 아무 곳에나 놓는다 — 놓은 자리는 랙만 쓴다. SO_EnemySkillCard.</summary>
    Enemies,
    /// <summary>판 위 아무 곳에나 놓으면 판 전체(올인 — 적이 아니라 주사위·목표물에).</summary>
    Board,
    /// <summary>주사위나 칩이 있는 칸 하나(캐시 아웃). 주사위 대상과 같이 칸을 겨눈다.</summary>
    Cell,
}

/// <summary>
/// 스킬 카드 한 종류(기획서 8). 쓰면 사라지는 1회용이다. 무엇을 하는지는 하위 클래스가 정하고(주사위 종류 SO와 같은 방식),
/// 쓸 수 있는지 판정은 SkillRules, 적용은 SkillCaster가 맡는다.
/// 이전의 즉시형 강화(페인트·다시 굴리기·잭팟)도 주사위 대상 스킬로 여기에 들어왔다 — 카드 종류는 놓는 곳 기준이다(5장).
/// </summary>
public abstract class SO_SkillCard : SO_Card
{
    [Tooltip("캐릭터 전용 카드(SO_HeroCharacter.ExclusiveCard)면 켠다 — 카드 종류가 전용(CardKind.Exclusive)이 되어 테두리 색·부제가 따로 나온다. 쓰는 법은 스킬과 같다.")]
    public bool Exclusive;

    public abstract SkillTarget Target { get; }
}

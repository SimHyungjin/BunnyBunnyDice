using UnityEngine;

/// <summary>
/// 영웅 특성 하나(기획서 10.3 — 캐릭터당 하나). 캐릭터 SO가 참조하고, 비우면 특성 없음(토끼공듀처럼 능력치로 승부하는 캐릭터).
///
/// 특성은 개발 중에 많이 생기고 엎어질 수 있어, 게임 시스템이 특성 종류를 알지 못하게 만든다:
/// - 끼어들 자리가 필요한 시스템은 자기 폴더에 작은 갈고리 인터페이스를 둔다(예: 주사위 굴림 IDicePipRoller).
/// - 특성은 이 클래스를 잇고 필요한 갈고리만 구현한다. 시스템은 TraitHooks.Find로 갈고리를 묻고, 없으면 원래대로 한다.
/// 그래서 있는 갈고리를 쓰는 특성은 클래스 하나만 더하면 되고, 지울 때도 그 클래스만 지우면 된다.
/// </summary>
public abstract class SO_HeroTrait : SO_Config
{
    [Header("특성")]
    [Localized] public string DisplayName = "";
    [Tooltip("숫자는 적지 않는다 — {필드} 토큰으로 적으면 SO 값으로 채운다(CardText).")]
    [Localized, TextArea] public string Description = "";
}

using UnityEngine;

/// <summary>
/// 캐릭터 선택 창 설정(기획서 12.3). 얼굴은 캐릭터 그림(SO_HeroCharacter.Portrait)이고, 자르는 값도 캐릭터 SO에 있다.
/// 칸 배치(열 수·칸 비율)는 UI 배치 규칙대로 프리팹(UI_CharacterSelectPopup)에 있다.
/// </summary>
[CreateAssetMenu(menuName = "Def/CharacterSelect/Character Select Config", fileName = nameof(SO_CharacterSelectConfig))]
public sealed class SO_CharacterSelectConfig : SO_Config
{
    [Header("창")]
    [Tooltip("뒤를 덮는 검은 판의 불투명도(Core 기본은 SO_CoreConfig.PopupGuardAlpha). 뒤의 로비 로고·패널이 창과 겹쳐 보이지 않게 진하게 둔다.")]
    [Range(0f, 1f)] public float GuardAlpha = 0.88f;
}

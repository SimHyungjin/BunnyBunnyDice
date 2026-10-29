using UnityEngine;

/// <summary>
/// 스킬 발동 컷인 설정(기획서 12.3). 화면 왼쪽에 비스듬한 판이 가운데 사선에서 펼쳐지고(얼굴 그림), 잠깐 머문 뒤 다시 접힌다
/// — 게임은 멈추지 않는다(표시만). 어느 카드에 띄울지는 SkillCutIn이, 판·그림·글자의 자리는 프리팹 앵커가 정한다.
/// 판 테두리 줄 색은 카드 종류 색(SO_CardHandConfig)을 따른다 — 컷인만 보고도 어떤 종류 카드를 썼는지 읽히게.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Skill Cut In Config", fileName = nameof(SO_SkillCutInConfig))]
public sealed class SO_SkillCutInConfig : SO_Config
{
    [Tooltip("판이 펼쳐지는 시간(초, 실제 시간 — 멈춤·배속과 무관). 그림은 펼쳐지는 동안 제자리에서 드러난다.")]
    [Min(0.01f)] public float InDuration = 0.12f;
    [Tooltip("다 펼쳐진 채 머무는 시간(초).")]
    [Min(0f)] public float HoldDuration = 0.45f;
    [Tooltip("다시 접혀 들어가는 시간(초). 카드 이름은 이 시간의 앞 절반에 흐려진다.")]
    [Min(0.01f)] public float OutDuration = 0.15f;
    [Tooltip("카드 이름이 드러나는 시간(초) — 판이 다 펼쳐진 뒤부터.")]
    [Min(0.01f)] public float TitleFadeDuration = 0.1f;
    [Tooltip("뒤 글자(SKILL)의 투명도 — 카드 종류 색에 곱한다. 그림보다 앞에 나서지 않게 연하게.")]
    [Range(0f, 1f)] public float BackTextAlpha = 0.28f;
}

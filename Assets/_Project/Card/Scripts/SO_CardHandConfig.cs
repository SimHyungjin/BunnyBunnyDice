using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 손패 설정. 손패 카드 높이는 화면 짧은 변의 비율이고(CardSizing), 손패의 거리 값은 모두 손패 카드 높이의 배수다
/// — 화면 크기·방향이 바뀌어도 손패 모양이 비율 그대로 유지되게. 태그 여백처럼 카드 안쪽 값은 카드 프리팹 단위다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Card/Card Hand Config", fileName = nameof(SO_CardHandConfig))]
public sealed class SO_CardHandConfig : SO_Config
{
    public CardView CardPrefab;
    [Tooltip("판 시작 시 손패. 같은 카드를 여러 번 넣으면 겹친다.")]
    public List<DiceCard> StartHand = new List<DiceCard>();
    [Tooltip("손패 상한 — 장수가 아니라 뭉치(겹친 카드 한 묶음) 수. 새 종류가 들어와 넘치면 기존 뭉치 하나가 무작위로 통째로 사라진다(기획서 8). 테스트 동안은 크게 열어 둔다.")]
    [Min(1)] public int MaxStacks = 99;

    [Header("카드")]
    [Tooltip("손패 카드 높이 = 화면 짧은 변 × 이 값. 다른 크기 값(집었을 때·레벨업 등)은 모두 손패 카드 = 1 기준이다.")]
    [Range(0.05f, 0.5f)] public float CardHeightRatio = 0.222f;
    [Tooltip("주사위 카드의 종류 색(테두리·각성 칸). 카드 종류(주사위·강화·스킬·플레이어 강화)마다 다르게 둔다 — 주사위 색과 헷갈리지 않는 색으로.")]
    public Color DiceCardColor = new Color(0.961f, 0.02f, 0.424f);
    [Tooltip("스킬 카드 테두리 색. 주사위(자홍)·칩(금색)과 겹치지 않는 민트.")]
    public Color SkillCardColor = new Color(0.25f, 0.9f, 0.7f);
    [Tooltip("칩 카드 테두리 색. 칩 7색과 겹치지 않는 금색.")]
    public Color ChipCardColor = new Color(1f, 0.729f, 0.2f);
    [Tooltip("영웅 강화 카드 테두리 색. 주사위(자홍)·스킬(민트)·칩(금색)과 겹치지 않는 하늘색.")]
    public Color HeroCardColor = new Color(0.35f, 0.65f, 1f);
    [Tooltip("캐릭터 전용 카드 테두리 색. 주사위(자홍)·스킬(민트)·칩(금색)·영웅(하늘)과 겹치지 않는 보라 — 판을 뒤집는 카드라 눈에 띄게.")]
    public Color ExclusiveCardColor = new Color(0.62f, 0.36f, 1f);
    [Tooltip("스킬 카드 부제.")]
    [Localized] public string SkillSubText = "스킬 카드";
    [Tooltip("칩 카드 부제.")]
    [Localized] public string ChipSubText = "칩 카드";
    [Tooltip("영웅 강화 카드 부제.")]
    [Localized] public string HeroSubText = "영웅 강화";
    [Tooltip("캐릭터 전용 카드 부제.")]
    [Localized] public string ExclusiveSubText = "전용 카드";
    [Tooltip("칩·스킬 카드의 종류 칸(주사위 카드의 각성 칸 자리). 어디에 놓는지가 고르는 기준이라 크게 적는다(기획서 5장).")]
    [UnityEngine.Serialization.FormerlySerializedAs("AttachLabel")]
    [Localized] public string ChipKindLabel = "칩";
    [UnityEngine.Serialization.FormerlySerializedAs("AttachText")]
    [Localized, TextArea] public string ChipKindText = "칸에 놓는다. 칩 위 주사위가 효과를 받는다. 빈 칸에 놓으면 길을 막는다. 칸마다 하나만 놓을 수 있다.";
    [Tooltip("스킬 카드의 종류 칸 — 쓰는 곳(SkillTarget)마다.")]
    [Localized] public string SkillDiceLabel = "주사위";
    [Localized, TextArea] public string SkillDiceText = "판 위 주사위 하나에 놓아 쓴다.";
    [Localized] public string SkillEnemiesLabel = "범위";
    [Localized, TextArea] public string SkillEnemiesText = "판 위에 놓으면 그 자리 원 안에 쓴다.";
    [Localized] public string SkillBoardLabel = "판 전체";
    [Localized, TextArea] public string SkillBoardText = "판 위 아무 곳에나 놓으면 판 전체에 쓴다.";
    [Localized] public string SkillCellLabel = "칸";
    [Localized, TextArea] public string SkillCellText = "주사위나 칩이 있는 칸에 놓아 쓴다.";
    [Tooltip("영웅 강화 카드의 종류 칸.")]
    [Localized] public string HeroKindLabel = "영웅";
    [Localized, TextArea] public string HeroKindText = "고르면 바로 이번 판 동안 영웅에게 적용된다. 손패에 들어가지 않는다.";
    [Tooltip("제목 뒤 띠의 불투명도(종류 색 위에).")]
    [Range(0f, 1f)] public float TitleBackAlpha = 0.25f;
    public Color StrongTagColor = new Color(0.247f, 0.878f, 1f);
    public Color StrongTagTextColor = Color.black;
    public Color WeakTagColor = new Color(0.353f, 0.227f, 0.416f);
    [Tooltip("약함 태그 등 어두운 띠 위의 글자 색.")]
    public Color TextColor = new Color(0.984f, 0.933f, 0.988f);
    [Tooltip("태그 글자 양옆 여백(카드 프리팹 단위).")]
    [Min(0f)] public float TagPadding = 8f;
    [Tooltip("태그 사이 간격.")]
    [Min(0f)] public float TagGap = 8f;

    [Header("누르고 있을 때(설명 보기)")]
    [Tooltip("끌기 전까지 누르고 있으면 이 크기로 커져 설명을 읽을 수 있다.")]
    [Min(0.1f)] public float InspectScale = 2.2f;
    [Tooltip("누른 자리에서 카드 중심까지(손패 카드 높이 배수) — 손가락이 카드를 가리지 않게 위로 띄운다.")]
    public Vector2 InspectOffset = new Vector2(0f, 1.5f);

    [Header("부채꼴")]
    [Tooltip("부채꼴 원의 반지름(손패 카드 높이 배수). 클수록 카드가 평평하게 펼쳐진다.")]
    public float FanRadius = 6.67f;
    [Tooltip("카드 사이 각도. 장수가 늘면 이 간격으로 벌어지다가 최대 각도에서 멈추고, 그 뒤로는 겹친다.")]
    public float AnglePerCard = 5f;
    public float MaxFanAngle = 20f;
    [Tooltip("가운데 카드의 중심 높이(화면 아래 기준, 손패 카드 높이 배수).")]
    public float HandHeight = 0.54f;

    [Header("집었을 때")]
    [Tooltip("카드를 집는 동안 손패를 이만큼 내려 맵을 덜 가린다(손패 카드 높이 배수).")]
    public float LowerOnHold = 0.375f;
    public float HoldScale = 1.15f;

    [Header("연출")]
    [Tooltip("목표 자세로 따라가는 빠르기. 클수록 빨리 붙는다.")]
    public float FollowSharpness = 14f;
    [Tooltip("새로 얻은 카드가 날아오기 시작하는 위치(손패 기준, 손패 카드 높이 배수)와 크기 — 획득을 눈에 띄게.")]
    public Vector2 NewCardFrom = new Vector2(0f, 2.25f);
    public float NewCardFromScale = 1.3f;
}

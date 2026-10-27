using UnityEngine;

/// <summary>
/// 영웅이 던지는 카드(표시만) 설정. 맞히기는 카드가 손을 떠나는 순간 HeroAttack이 끝내고, 카드는 그 뒤 아주 빠르게 날아가
/// 판정과 어긋남이 눈에 띄지 않게 한다. 크기는 칸 단위(한 칸 = 1), 시간은 초다.
/// 영웅 공격은 주사위 공격선(쏜 주사위 색)과 겹치지 않게 흰색·금색이다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Hero/Hero Card Shot Config", fileName = nameof(SO_HeroCardShotConfig))]
public sealed class SO_HeroCardShotConfig : SO_Config
{
    [Header("카드")]
    [Tooltip("카드 판 머티리얼. 색(_BaseColor)을 입히므로 URP Unlit 계열이어야 한다. 카드 그림이 생기면 그 머티리얼로 바꾼다.")]
    public Material CardMaterial;
    [Tooltip("카드 크기(가로, 세로 — 칸).")]
    public Vector2 CardSize = new Vector2(0.12f, 0.17f);
    [Tooltip("손에서 적까지 날아가는 시간. 판정은 손을 떠날 때 끝나므로 짧을수록 어긋남이 안 보인다.")]
    [Min(0.01f)] public float FlightTime = 0.13f;
    [Tooltip("휘는 정도(손~적 거리 배수). 카드는 손이 몸에서 뻗은 쪽으로 먼저 튀어 나갔다가 휘어 적에게 꽂힌다 — 0이면 곧게 난다.")]
    [Min(0f)] public float CurveBend = 0.4f;
    [Tooltip("한 번에 여러 장 던질 때 이웃 카드끼리 튀어 나가는 방향을 벌리는 각도(도). 같은 적에게 몰려도 여러 갈래로 휘어 들어가 장수가 보이게.")]
    [Range(0f, 90f)] public float SpreadAngle = 20f;
    [Tooltip("날아가며 도는 빠르기(도/초). 바닥과 나란히 누워 돈다.")]
    public float SpinSpeed = 1440f;

    [Header("색")]
    public Color Color = new Color(0.984f, 0.933f, 0.988f);
    [Tooltip("치명타 카드 색(금색).")]
    public Color CritColor = new Color(1f, 0.88f, 0.29f);
    [Tooltip("색에 곱하는 밝기. 1을 넘으면 블룸으로 빛난다.")]
    [Min(0f)] public float Intensity = 2f;
    [Tooltip("치명타 카드 크기 배율.")]
    [Min(1f)] public float CritSizeScale = 1.4f;

    [Header("꼬리")]
    [Tooltip("꼬리 머티리얼. 정점 색을 곱하는 반투명 셰이더여야 끝으로 갈수록 흐려진다.")]
    public Material TrailMaterial;
    [Tooltip("꼬리가 남는 시간.")]
    [Min(0.01f)] public float TrailTime = 0.08f;
    public float TrailWidth = 0.06f;

    [Tooltip("처음 잡아 두는 카드 수. 모자라면 더 만든다.")]
    [Min(1)] public int InitialCapacity = 8;
}

using TMPro;
using UnityEngine;

/// <summary>
/// 피해 숫자(맞은 몬스터 위로 튀어 오르는 숫자) 설정. 수치를 맞출 때 무엇이 얼마나 들어가는지 보려고 둔다.
/// 크기는 칸 단위(한 칸 = 1), 시간은 초다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Combat/Damage Number Config", fileName = nameof(SO_DamageNumberConfig))]
public sealed class SO_DamageNumberConfig : SO_Config
{
    [Tooltip("끄면 숫자를 띄우지 않는다. 몬스터가 많아 화면이 어지러우면 끈다.")]
    public bool Enabled = true;

    [Header("글자")]
    public TMP_FontAsset Font;
    [Tooltip("외곽선이 있는 머티리얼 — 맵 색과 겹쳐도 읽히게.")]
    public Material Material;
    public float FontSize = 3f;
    [Tooltip("치명타 숫자의 크기 배율.")]
    [Min(1f)] public float CriticalScale = 1.5f;

    [Header("색")]
    public Color NormalColor = Color.white;
    public Color CriticalColor = new Color(1f, 0.6f, 0.15f);
    [Tooltip("방어력에 깎여 원래보다 적게 들어간 피해.")]
    public Color ReducedColor = new Color(0.6f, 0.6f, 0.65f);
    [Tooltip("화상(지속 피해). 화상 칩 색(자홍)과 같게 — 칩·상태 네모·숫자가 한 색이게(기획서 7).")]
    public Color BurnColor = new Color(0.9f, 0.25f, 0.75f);
    [Tooltip("더블 업으로 더 받은 경험치(\"+N\"). 금색 — 피해 숫자와 구분된다. 크기는 치명타 배율.")]
    public Color BonusExpColor = new Color(1f, 0.85f, 0.25f);

    [Header("튀어 오르기")]
    [Min(0.05f)] public float Duration = 0.8f;
    [Tooltip("몬스터 발밑에서 처음 뜨는 높이.")]
    public float StartHeight = 0.6f;
    [Tooltip("처음 위로 튀는 빠르기(초당 칸). 이 사이에서 무작위 — 숫자마다 높이가 달라 겹치지 않게.")]
    public Vector2 LaunchSpeed = new Vector2(2.5f, 3.5f);
    [Tooltip("화면 좌우로 튀는 최대 빠르기(초당 칸). 왼쪽·오른쪽 무작위.")]
    public float SideSpeed = 1.2f;
    [Tooltip("떨어지게 당기는 힘(초당 칸²).")]
    public float Gravity = 9f;

    [Header("늘었다 찌그러지기")]
    [Tooltip("올라갈 때 세로로 늘고 가로로 좁아지며, 떨어질 때 반대가 되는 정도. 0이면 모양이 그대로다.")]
    [Range(0f, 0.6f)] public float Stretch = 0.3f;
    [Tooltip("처음 0에서 제 크기로 튀어나오는 시간.")]
    [Min(0.01f)] public float PopDuration = 0.08f;
    [Tooltip("남은 시간이 이 비율 아래로 내려가면 흐려지기 시작한다.")]
    [Range(0.05f, 1f)] public float FadeRatio = 0.35f;

    [Tooltip("처음 잡아 두는 숫자 자리 수. 모자라면 늘어난다(개수 제한은 없다 — 막으면 후반에 숫자가 계속 갈려 읽히지 않는다).")]
    [Min(1)] public int InitialCapacity = 32;
}

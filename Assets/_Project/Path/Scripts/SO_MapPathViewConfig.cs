using UnityEngine;

/// <summary>
/// 출현 구역별 예상 경로 표시 설정. 경로 전체에 흐린 바탕선을 늘 그리고, 출현 구역에서 일정 간격으로 빛을 내보내 꼬리를 남기며 목표물까지 달리게 한다.
/// 위치·크기는 칸 단위(한 칸 = 1), 시간은 초다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Map/Map Path View Config", fileName = nameof(SO_MapPathViewConfig))]
public sealed class SO_MapPathViewConfig : SO_Config
{
    [Header("외형")]
    [Tooltip("달리는 빛 머리. 중심 피벗.")]
    public GameObject HeadPrefab;
    [Tooltip("꼬리 머티리얼. 정점 색(투명도)을 곱하는 반투명 셰이더여야 꼬리 끝이 흐려진다.")]
    public Material TrailMaterial;
    [Tooltip("출현 구역 순서대로 쓴다. 모자라면 처음부터 다시 쓴다. 밝기가 1을 넘으면 블룸으로 빛난다.")]
    [ColorUsage(false, true)]
    public Color[] PathColors =
    {
        new Color(2f, 1.7f, 0.4f), new Color(0.6f, 2f, 1f), new Color(2f, 0.8f, 1.8f), new Color(0.8f, 1.8f, 2f),
    };

    [Header("움직임")]
    [Tooltip("빛 머리가 달리는 빠르기(초당 칸 수).")]
    [Min(0.1f)] public float RunSpeed = 5f;
    [Tooltip("빛을 내보내는 간격. 꼬리 길이(TrailTime)보다 길게 두어야 꼬리끼리 겹쳐 하얗게 뜨지 않는다(경로가 끊겨 보이지 않는 것은 바탕선이 맡는다).")]
    [Min(0.1f)] public float SendInterval = 1.5f;

    [Header("꼬리")]
    [Tooltip("꼬리 길이(초). 머리가 지나간 자리가 이 시간 동안 남는다.")]
    [Min(0.05f)] public float TrailTime = 0.8f;
    public float TrailWidth = 0.22f;
    [Tooltip("꺾이는 곳을 둥글게 잇는 정점 수. 0이면 각지게 꺾인다.")]
    [Range(0, 8)] public int TrailCornerVertices = 3;

    [Header("바탕선")]
    [Tooltip("경로 전체에 늘 깔리는 흐린 선의 굵기.")]
    public float BaseLineWidth = 0.07f;
    [Tooltip("바탕선의 진하기(경로 색에 곱한다). 블룸으로 빛나지 않을 만큼 낮게 둔다.")]
    [Range(0f, 1f)] public float BaseLineAlpha = 0.35f;

    [Header("배치")]
    [Tooltip("바닥에서 띄우는 높이. 바닥과 겹쳐 깜빡이지 않게.")]
    public float Lift = 0.05f;

    public Color GetColor(int index) => PathColors != null && PathColors.Length > 0 ? PathColors[index % PathColors.Length] : Color.white;
}

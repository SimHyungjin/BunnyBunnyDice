using UnityEngine;

/// <summary>고정 시점 카메라 설정. 맵 전체가 한 화면(여백 제외)에 들어오도록 거리는 MapCameraFitter가 계산한다.</summary>
[CreateAssetMenu(menuName = "Def/Map/Map Camera Config", fileName = nameof(SO_MapCameraConfig))]
public sealed class SO_MapCameraConfig : SO_Config
{
    [Tooltip("수평 기준 기울기. 클수록 위에서 내려다본다 — 너무 눕히면 먼 줄의 칸이 작아져 집기 어렵다.")]
    [Range(30f, 90f)] public float TiltAngle = 57f;
    [Tooltip("수평 회전. 0이면 정면, 돌리면 판 모서리가 대각선으로 보여 입체감이 커진다 — 대신 가로로 긴 판은 화면에 담을 폭이 크게 늘어 칸이 작아진다(모바일에서는 0 권장).")]
    [Range(-90f, 90f)] public float YawAngle = 0f;
    [Range(20f, 60f)] public float FieldOfView = 30f;
    [Tooltip("먼 줄 위에 설 캐릭터 높이만큼 위쪽 여유를 둔다.")]
    public float CharacterHeadroom = 0.8f;

    [Header("화면 방향")]
    [Tooltip("화면의 긴 쪽과 맵의 긴 쪽이 다르면 카메라를 돌려 맞춘다(세로 화면에서는 맵을 세로로 본다).")]
    public bool AutoOrient = true;
    [Tooltip("방향을 맞출 때 더하는 수평 회전 각도.")]
    public float OrientYawOffset = 90f;

    [Header("화면 여백 — 가로 화면 (화면 비율)")]
    [Tooltip("상단 정보 바 자리.")]
    [Range(0f, 0.4f)] public float MarginTop = 0.12f;
    [Tooltip("카드 손패 자리. 판 아래 펠트·테두리(약 1칸) 위로 카드가 겹치므로 손패 높이 전부를 잡지 않아도 된다.")]
    [Range(0f, 0.4f)] public float MarginBottom = 0.25f;
    [Tooltip("손패가 비었을 때의 아래 여백. 카드가 없으면 손패 자리가 통째로 비므로 판을 그만큼 내려 넓힌다(HandFraming).")]
    [Range(0f, 0.4f)] public float EmptyHandMarginBottom = 0.1f;
    [Tooltip("노치 자리.")]
    [Range(0f, 0.4f)] public float MarginSide = 0.025f;

    [Header("화면 여백 — 세로 화면 (화면 비율)")]
    [Range(0f, 0.4f)] public float PortraitMarginTop = 0.08f;
    [Tooltip("세로 화면은 손패가 맵과 겹치지 않게 아래를 넉넉히 비운다.")]
    [Range(0f, 0.4f)] public float PortraitMarginBottom = 0.16f;
    [Tooltip("세로 화면에서 손패가 비었을 때의 아래 여백.")]
    [Range(0f, 0.4f)] public float PortraitEmptyHandMarginBottom = 0.16f;
    [Range(0f, 0.4f)] public float PortraitMarginSide = 0.04f;

    [Header("빈 손 구도 (HandFraming)")]
    [Tooltip("손패가 비었을 때의 기울기. 여백만 바꾸면 판이 그대로 미끄러져 내려와 어색하다 — 조금 더 내려다보며 내려오게 한다.\n" +
             "가로·세로 화면 모두에 쓴다.")]
    [Range(30f, 90f)] public float EmptyHandTiltAngle = 63f;
    [Tooltip("손패를 다 쓴 뒤 이만큼(초, 실제 시간) 기다렸다가 판을 내린다. 0 = 바로(사용자 결정).\n" +
             "옮기는 빠르기는 카드가 손패로 붙는 빠르기(SO_CardHandConfig.FollowSharpness)와 같다.")]
    [Min(0f)] public float EmptyHandDelay = 0f;

    [Header("거리 탐색")]
    public float SearchMinDistance = 1f;
    public float SearchMaxDistance = 200f;
    [Min(1)] public int SearchIterations = 30;
    [Tooltip("원근 카메라라 맵 중심을 여백 영역 중앙에 맞추는 보정을 한 번에 끝내지 못해 여러 번 한다.")]
    [Min(1)] public int RecenterPasses = 2;

    /// <summary>기울기(빈 손 구도로 넘어간 정도만큼 EmptyHandTiltAngle로 섞는다).</summary>
    public float GetTilt(float emptyHand) => Mathf.Lerp(TiltAngle, EmptyHandTiltAngle, emptyHand);

    /// <param name="emptyHand">빈 손 구도로 넘어간 정도(0~1, HandFraming.Weight). 여백은 아래만 섞는다.</param>
    public void GetMargins(bool portrait, float emptyHand, out float top, out float bottom, out float side)
    {
        top = portrait ? PortraitMarginTop : MarginTop;
        bottom = portrait
            ? Mathf.Lerp(PortraitMarginBottom, PortraitEmptyHandMarginBottom, emptyHand)
            : Mathf.Lerp(MarginBottom, EmptyHandMarginBottom, emptyHand);
        side = portrait ? PortraitMarginSide : MarginSide;
    }
}

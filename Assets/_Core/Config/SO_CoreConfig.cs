using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 코어 매니저들이 쓰는 기본값을 한곳에 모은 설정 에셋.
///
/// 예전에는 이 값들이 각 매니저 안의 <c>const</c>나 인라인 리터럴로 흩어져 있었다.
/// 게임마다 달라야 할 값(프레임레이트·연출 시간·풀 크기)이 코드에 박혀 있으면
/// 프로젝트를 옮길 때마다 코어를 고치게 되고, 무엇보다 <b>어디에 있는지 찾을 수가 없다.</b>
/// (서비스 레이어를 붙이면 그쪽 설정 SO_ServiceConfig가 같은 역할을 서비스에 대해 한다.)
///
/// 에셋이 없어도 부팅은 막지 않는다 — 아래 기본값이 곧 예전에 코드에 박혀 있던 값이라,
/// 에셋을 만들기 전에도 동작이 달라지지 않는다.
/// (서비스 설정과 달리 "빌드에서 전부 끄는" 안전장치가 없는 이유가 이것이다.
///  코어 기본값은 잘못 흘러가도 규제·수익 문제가 되지 않는다.)
///
/// <b>여기에 넣지 않는 것</b>: 저장 스키마 버전, 분석 이벤트 이름·원격 설정 키,
/// 암호화 규격처럼 <b>바깥과의 계약</b>인 값. 그것들은 코드가 정본이어야 하고
/// 에셋에서 바꿀 수 있으면 오히려 위험하다.
/// </summary>
[CreateAssetMenu(fileName = "SO_CoreConfig", menuName = "Core/CoreConfig")]
public class SO_CoreConfig : ScriptableObject
{
    private const string ResourceName = "SO_CoreConfig";

    private static SO_CoreConfig _instance;

    #region 화면 · 성능

    [Header("화면 · 성능")]
    [Tooltip("모바일의 목표 프레임레이트. Unity 기본값은 30이라 손대지 않으면 게임이 느려 보인다.")]
    public int MobileTargetFrameRate = 60;

    [Tooltip("기기 주사율을 읽지 못했을 때 데스크톱에서 쓸 값.")]
    public int FallbackRefreshRate = 60;

    [Tooltip("모바일에서 화면 자동 꺼짐을 막는다. 조작이 뜸한 구간에서 화면이 꺼지는 것을 방지한다.")]
    public bool PreventScreenSleepOnMobile = true;

    [Tooltip("세이프 에어리어에서 화면의 짧은 축 인셋을 무시한다(가로 화면이면 위아래, 세로 화면이면 좌우). " +
             "비대칭 여백을 피하려는 것이지만, 가로로 든 아이폰의 홈 인디케이터(아래) 영역까지 무시되어 UI가 겹칠 수 있다. " +
             "그 영역을 피해야 하는 UI가 있으면 끈다.")]
    public bool SafeAreaIgnoreMinorAxis = true;

    [Header("입력")]
    [Tooltip("ESC·Android 뒤로가기 키를 뒤로가기(App.HandleBackPressed)로 보내는 코어 입력 모듈을 부팅 때 켠다. " +
             "게임이 뒤로가기를 직접 처리하려면 끈다.")]
    public bool EnableBackInput = true;

    #endregion

    #region 진동

    [Header("진동 — Android 지속시간(ms)")]
    [Tooltip("Android는 지속 시간으로만 세기를 표현한다. iOS는 시스템 임팩트 스타일을 쓰므로 이 값과 무관하다.")]
    public int HapticWeakMs   = 20;
    public int HapticSoftMs   = 30;
    public int HapticMediumMs = 60;
    public int HapticHardMs   = 100;

    #endregion

    #region UI 레이어

    [Header("UI 레이어 sortingOrder")]
    // 레이어 사이 간격이 넉넉해야 한 레이어 안에서 팝업을 여러 장 쌓아도 다음 레이어를 침범하지 않는다.
    [Tooltip("월드 위 UI(이름표·데미지 숫자). 가장 아래 층.")]
    [FormerlySerializedAs("OverlayOrder")] public int WorldOrder = 0;
    public int HudOrder     = 1000;
    public int PopupOrder   = 2000;
    [Tooltip("전환 커튼·로딩 화면. 가장 위 층.")]
    [FormerlySerializedAs("SceneOrder")] public int TransitionOrder = 10000;

    [Tooltip("팝업 한 장마다 더해지는 sortingOrder 간격. 팝업 내부 요소가 몇 겹까지 겹칠 수 있는지를 정한다.")]
    public int PopupOrderStep = 10;

    #endregion

    #region UI 연출

    [Header("UI 연출")]
    [Tooltip("팝업·HUD 등장/퇴장 애니메이션의 기본 길이(초).")]
    public float UIAnimationDuration = 0.30f;

    [Tooltip("팝업 뒤 클릭 가드 패널이 페이드되는 시간(초).")]
    public float PopupGuardFadeDuration = 0.2f;

    [Range(0f, 1f)]
    [Tooltip("클릭 가드 패널의 기본 불투명도. ShowPopup에서 따로 지정하지 않았을 때 쓰인다.")]
    public float PopupGuardAlpha = 0.6f;

    [Tooltip("닫기 애니메이션이 끝나지 않아도 강제로 정리하기까지의 시간(초). " +
             "연출이 중간에 멈춰도 팝업이 화면에 남아 조작을 막는 일이 없도록 하는 안전장치다.")]
    public float PopupCloseTimeout = 3f;

    #endregion

    #region 오디오

    [Header("오디오")]
    [Tooltip("효과음 소스 수(고정). 모자라면 덜 중요하고 오래된 소리를 빼앗아 쓴다. 늘리지 않는다.")]
    public int SfxPoolSize = 16;

    [Tooltip("배경음을 바꿀 때 겹쳐 트는 기본 시간(초).")]
    public float BgmCrossfadeDuration = 1f;

    [Tooltip("배경음을 멈출 때 기본 페이드아웃 시간(초).")]
    public float BgmStopFadeDuration = 0.5f;

    #endregion

    #region 데이터

    [Header("세이브")]
    [Tooltip("바뀐 섹션이 있을 때 자동 저장하는 간격(초, 실제 시간). 0이면 자동 저장을 끈다. " +
             "백그라운드 전환·씬 전환 저장과 별개로, 비정상 종료 시 잃는 범위를 줄이는 안전망이다.")]
    public float DataAutoSaveInterval = 30f;

    #endregion

    #region 번역

    [Header("번역")]
    [Tooltip("번역표 CSV의 Addressables 주소. 비우거나 등록하지 않으면 원문만 보인다. 표는 Tools/Localization/Update Table이 만들고 고친다.")]
    public string LocalizationTableKey = "LocalizationTable";

    [Tooltip("원문 언어 코드 — 게임 데이터(SO·프리팹)에 적는 언어. 번역표가 있으면 표의 첫 열 이름을 따른다.")]
    public string SourceLanguage = "ko";

    [Tooltip("기기 언어가 번역표에 없을 때 보일 언어 코드. 이것도 표에 없으면 원문 언어.")]
    public string FallbackLanguage = "en";

    #endregion

    #region 풀

    [Header("오브젝트 풀")]
    [Tooltip("풀 하나가 보관하는 최대 인스턴스 수. 넘으면 반납된 것을 파괴한다.")]
    public int PoolDefaultMaxSize = 1024;

    #endregion

    /// <summary>
    /// 설정 에셋. 없으면 기본값 인스턴스로 대신한다.
    ///
    /// 에디터에서만 안내를 남긴다. 빌드에서까지 경고를 띄우지 않는 이유는,
    /// 기본값이 예전에 코드에 박혀 있던 값 그대로라 에셋이 없어도 동작이 정상이기 때문이다.
    /// </summary>
    public static SO_CoreConfig Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = Resources.Load<SO_CoreConfig>(ResourceName);
            if (_instance != null) return _instance;

#if UNITY_EDITOR
            Debug.Log(
                $"[CoreConfig] Resources에서 {ResourceName}을(를) 찾지 못해 기본값으로 동작합니다. " +
                "값을 조정하려면 메뉴의 Create > Core > CoreConfig로 만들어 Resources 폴더에 두세요.");
#endif
            _instance = CreateInstance<SO_CoreConfig>();
            return _instance;
        }
    }

    // "도메인 리로드 없이 플레이"에서 이전 플레이의 캐시가 남지 않도록 Core가 불러준다.
    internal static void ResetStatics() => _instance = null;
}

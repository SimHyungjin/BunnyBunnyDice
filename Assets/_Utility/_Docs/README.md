# _Utility

프로젝트 간 재사용하는 에디터 툴·실행 중 조정·UI 컴포넌트·셰이더. 이 폴더(문서 포함)는 다른 프로젝트에 통째로 복사해 쓴다.

## 구성 — 기능별 폴더

**기능마다 폴더 하나를 두고, 에디터 전용 코드는 그 안의 `Editor/`에 둔다.** 에디터 코드를 한 `Editor/`에 모으면 툴이 늘수록 무엇이 무엇에 딸렸는지 알 수 없다. Unity는 어느 깊이의 `Editor/` 폴더든 에디터 전용으로 컴파일한다(`Assembly-CSharp-Editor`, asmdef가 있는 폴더만 예외).

| 폴더 | 내용 |
|---|---|
| `Tuning/` | 설정 SO 실행 중 조정(`SO_Config`·`ConfigTuning`·`GameTuning`) + `Editor/`(검색·원본 열기 인스펙터) |
| `InlineScriptableObject/` | `[InlineScriptableObject]` 속성 + `Editor/`(그리는 쪽) |
| `PackageInstaller/Editor/` | Tools/Settings/Package Installer(`Scripts/`·`Data/`) |
| `CustomSymbol/Editor/` | Tools/Settings/Custom Symbols(`Scripts/`·`Data/`) |
| `AddressableAutoName/Editor/` | Addressable 자동 이름(전용 asmdef — 선택 패키지) |
| `Transform/Editor/` | Tools/Transform/* |
| `Model/Editor/` | Tools/Model/* |
| `UI/` | uGUI 컴포넌트(런타임만, 에디터 코드 없음) |
| `Shader/` | Shader Graph(`Fill`, `WorldSpaceUV`) |
| `_Shared/Editor/` | 툴 여럿이 같이 쓰는 것(`UtilityMenuPriority`) |

## 규칙

- **`_Utility` 코드는 `_Project`·`_Core`의 코드를 참조하면 안 된다.** 다른 프로젝트로 복사했을 때 깨진다. `Assembly-CSharp-Editor`는 게임 코드를 자동 참조하므로 컴파일러가 막아 주지 않는다 — 직접 지켜야 한다.
- **새 기능은 기능 폴더를 만들고, 에디터 코드는 그 안의 `Editor/`에 둔다.** 런타임 코드가 없는 툴은 `<기능>/Editor/`만 있다. 툴이 데이터를 가지면 `<기능>/Editor/Scripts/`와 `<기능>/Editor/Data/`로 나눈다.
- **선택 패키지에 의존하는 코드는 그 기능 폴더에 전용 asmdef를 둔다.** 그 asmdef에 `versionDefines`(패키지가 설치되면 심볼 정의)와 같은 심볼의 `defineConstraints`를 함께 넣는다. 패키지가 없으면 어셈블리 전체가 컴파일에서 빠지므로 코드에 `#if`가 필요 없다. 이 asmdef는 `Assembly-CSharp-Editor`를 참조할 수 없어 `_Shared/Editor/`도 쓰지 못한다.
  - 예: `AddressableAutoName/Editor/Utility.Editor.Addressables.asmdef` — `com.unity.addressables` → `UTILITY_ADDRESSABLES`
- 에디터 메뉴는 `Tools/<카테고리>/<이름>` 경로를 쓰고, 우선순위는 `UtilityMenuPriority`(`_Shared/Editor/UtilityMenuPriority.cs`)의 상수(+오프셋)를 쓴다.
- 클래스명 접두사: `Window_*`(EditorWindow), `Editor_*`(`[InitializeOnLoad]` 훅), `SO_*`(ScriptableObject), `UI_Utility_*`(UI 컴포넌트). 인스펙터(CustomEditor)는 `<대상>Inspector`.
- 툴이 에셋을 EditorPrefs에 경로로 기억하면, 경로가 낡았을 때(폴더 정리) 타입으로 다시 찾는다(`FindAssets("t:...")`).
- SO 에셋은 프로젝트 간에 복사되므로 프로젝트별 상태(설치 여부 등)를 넣지 않는다. 그런 상태는 EditorPrefs에 프로젝트 경로별 키로 둔다.

## 에디터 툴

- **Tools/Settings/Package Installer** — `SO_PackageData` 에셋으로 패키지를 관리·설치한다.
  - "외부 패키지" 탭(`packages`): UPM(Git URL) / OpenUPM / .unitypackage URL. (UniTask, UTS3, Outline 등)
  - "Unity 패키지" 탭(`favorites`): Unity Registry / My Assets(에셋스토어). `id`(패키지 ID)가 있으면 ID로 설치 여부를 감지하고, UnityRegistry 항목은 `Client.Add(id)`로 설치한다. MyAssets는 다운로드 API가 없으므로 Asset Store 캐시(`%APPDATA%\Unity\Asset Store-5.x`, `ASSETSTORE_CACHE_PATH`)에 있으면 Import, 없으면 Package Manager 창을 연다. 감지되지 않는 MyAssets 항목의 수동 체크는 EditorPrefs에 프로젝트 경로별로 저장한다.
  - 패키지 설치 후 도메인 리로드가 일어나므로 창의 캐시·설치 상태 필드는 `[NonSerialized]`다.
- **Tools/Settings/Custom Symbols** — `SO_CustomSymbol` 에셋의 정의 심볼을 플랫폼별로 Player Settings에 반영한다. 정의 심볼은 Player Settings를 직접 고치지 말고 이 에셋에 등록한다. (플랫폼 단위라 같은 플랫폼에서 Dev/Live를 나누려면 Build Profile을 쓴다)
- **Addressable 자동 이름**(`AddressableAutoName/Editor/Editor_AddressableAutoName`) — 에셋을 Addressable로 등록하면 주소를 에셋 이름으로 바꾼다(중복이면 `_1`, `_2`…). `AddressableAssetSettings.OnModificationGlobal`을 구독하므로 설정이 나중에 만들어져도 동작한다.
- **Tools/Transform/Move Selected** — 선택한 씬 오브젝트(최상위만)를 오프셋만큼 이동. 프리팹 에셋은 건드리지 않는다.
- **Tools/Transform/Round Selected Transform** — 위치·회전은 정수, 스케일은 소수점 2자리로 반올림(AwayFromZero).
- **Tools/Model/Batch Extract Materials from FBX** — 선택한 FBX들의 머티리얼을 추출하고 리매핑한다.
- **Tools/Model/AnimationInPlace** — 독립 `.anim` 클립(Humanoid)의 루트 XZ 이동을 0으로 만든 복사본을 만든다. FBX 안의 클립은 Ctrl+D로 먼저 추출한다. Generic 리그는 지원하지 않는다.

## 설정 SO 실행 중 조정 (`Tuning/`)

설정 SO를 플레이 중에 맞추는 틀. 게임은 원본 에셋 대신 실행 중 복사본을 쓴다.

| 고치는 곳 | 저장 | 플레이에 적용 |
|---|---|---|
| `GameTuning` 인스펙터(복사본) | 이번 플레이만, 원본 SO에는 쓰지 않는다 | 바로 |
| 원본 SO 에셋 | 영구 | 바로 — 그 SO의 복사본은 원본 값으로 통째로 덮인다 |

- **`SO_Config`** — 설정 SO의 부모. 값이 바뀌면 `Changed`(인스펙터 수정은 OnValidate로, 코드로 덮어쓴 뒤에는 `NotifyChanged`). 다른 설정 SO를 참조하면 `SwapNested`를 override해 하위도 복사본으로 바꿔 끼운다.
- **`ConfigTuning`** — `Copy(원본)`이 복사본을 만들어 돌려준다(하위 설정까지). 에디터에서는 원본이 바뀌면 복사본에 다시 덮어쓰고 `Changed`를 부른다. `Dispose`로 복사본을 지우고 원본 구독을 푼다(원본은 에셋이라 플레이가 끝나도 남는다).
- **`GameTuning`** — 복사본을 펼쳐 보이는 오브젝트. 씬 진입 코드가 `new GameObject("GameTuning").AddComponent<GameTuning>().Show(tuning)`으로 만든다.
- **인스펙터**(`Editor/GameTuningInspector`) — 검색창(설정 이름·필드 이름·툴팁, 띄어쓰기로 낱말을 나누면 모두 맞아야 한다. 예: `lobby tilt`)과 설정마다 **원본 열기**(Project의 원본 에셋을 선택).
- 쓰는 쪽은 값을 복사해 두지 말고 복사본 참조를 들고 매번 읽거나, `Changed`를 구독해 다시 적용한다.

## 인스펙터 표시

- **`[InlineScriptableObject]`** (`InlineScriptableObject/InlineScriptableObjectAttribute` + `InlineScriptableObject/Editor/PropertyDrawer_InlineScriptableObject`) — ScriptableObject 참조 필드 아래에 대상 SO의 필드를 접고 펼 수 있게 그린다. 리스트에 붙이면 요소마다 적용되고 라벨은 대상 이름이 된다. 고친 값은 대상의 SerializedObject로 적용하므로 Undo가 되고 대상의 OnValidate가 불린다.

## UI 컴포넌트

- **`UI_Utility_FlexLayout` + `UI_Utility_FlexLayoutElement`** — 한 축 배치 레이아웃. 요소 타입 `Aspect`(교차축 길이 × value) / `Percent`(가용 길이 × value) / `Fill`(남은 공간을 가중치로). 방향·간격 비율·패딩·정렬·역순·부족 시 비율 유지 축소. Element 없는 자식은 `Aspect 1`. preferred size를 계산하지 않으므로 Content Size Fitter와 함께 쓰지 않는다.
- **`UI_Utility_Quad`** — 네 꼭짓점을 따로 정하는 사각형 도형(`MaskableGraphic`). 기울어진 판·비뚤어진 띠를 겹쳐 만드는 UI에 쓴다. 꼭짓점은 Rect 안의 비율(0~1, 왼쪽 아래 기준)이라 CSS `clip-path: polygon(x% y%)`는 `(x, 1 − y)`로 옮기면 된다. 색은 `Graphic.color`. 위·아래 투명도 배수(`_topAlpha`/`_bottomAlpha`)를 다르게 주면 세로 그러데이션이 된다.
- **`UI_Utility_OrientationAnchors`** — 가로·세로 화면에서 요소의 앵커를 바꿔 끼운다. 요소마다 가로용·세로용 자리(빈 RectTransform, 요소와 같은 부모)를 두면 붙은 영역이 세로로 길 때 세로 자리의 앵커를, 아니면 가로 자리의 앵커를 요소에 복사한다(offset 0). 부모를 바꾸지 않고 앵커만 옮긴다(크기 변경 콜백 안의 SetParent는 레이아웃 재계산과 부딪힌다). 실행 중에만 동작한다.
- **`UI_Utility_TextWordStep`** — TMP 글자를 띄어쓰기로 나뉜 낱말 순서대로 조금씩 내린다(계단진 로고·제목). 꼭짓점을 옮기므로 외곽선·그림자(Outline·Underlay)도 함께 내려간다. 내리는 양은 글자 크기 비율(`_stepRatio`, 음수면 올라감)이라 Auto Size에도 같은 모양이고, 글 안에 태그를 넣지 않아 번역과 무관하게 낱말 수대로 계단이 생긴다. 편집 중에도 보인다.
  - 낱말 판(선택): `_plateTemplate`(글자와 같은 부모, 글자보다 앞 순서)를 주면 실행 중 낱말마다 판을 깐다 — 판도 계단진다. 견본의 **자식 하나가 층 하나**(그림자·테두리·본체 순)이고, 층마다 모든 낱말의 판을 한꺼번에 그린다 — 겹친 곳은 본체가 옆 판의 테두리를 덮어 한 덩어리 띠가 된다(이웃과 맞닿는 안쪽 끝은 `_plateJoin`만큼 이웃 낱말 안으로 뻗어 본체끼리 넉넉히 겹친다 — 판 끝이 비스듬해도 이음매가 보이지 않게. 바깥 끝은 `_platePadding`). 크기는 낱말 범위 + 여백(글자 크기 비율)으로 계산하고, 결과는 부모 대비 앵커 비율(offset 0)로 넣는다. 편집 중에는 견본이 띠 전체로 보인다. 판 배치는 LateUpdate에서 한다(글자 메시를 다시 만드는 도중에 오브젝트를 만들면 캔버스 재구성과 부딪힌다).
- **`UI_Utility_DisabledDim`** — 버튼(Selectable)이 꺼지면 버튼 안의 그림·글자를 모두 어둡게 한다(밝기 배수, 기본 0.45, 알파는 그대로). Selectable의 색 전환은 대상 그래픽 하나만 칠하므로 테두리·몸통·글자를 겹친 버튼은 꺼져도 겉모습이 거의 그대로다. 반투명(CanvasGroup 알파)으로 하면 겹친 몸통이 비쳐 아래 판이 드러나 쓰지 않는다. 버튼의 Disabled Color는 흰색으로 둔다(두 번 어두워지지 않게). 꺼지는 순간의 색을 기억했다 되돌린다. interactable 변경 알림이 없어 LateUpdate에서 확인한다.
- **`UI_Utility_AlphaRaycastTarget`** — Image의 투명 영역을 레이캐스트에서 제외(`alphaHitTestMinimumThreshold`). 텍스처 Read/Write가 필요해 CPU 메모리를 더 쓰므로, 큰 텍스처·아틀라스에는 Sprite의 Custom Physics Shape를 고려한다. 스프라이트를 런타임에 바꿨다면 `Apply()`.

## 남은 정리

- Package Installer(카테고리·필터·OpenUPM scope)와 Custom Symbols에 기존 LINQ가 남아 있다(프로젝트 컨벤션상 LINQ를 쓰지 않음).

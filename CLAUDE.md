# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 먼저 읽을 문서

**`_Core`나 `_Utility`의 코드를 읽거나 고치기 전에, 해당 폴더의 `_Docs/README.md`를 먼저 읽는다.**

- `Assets/_Core/_Docs/README.md` — 런타임 프레임워크(부팅·매니저·씬·리소스/풀·세이브·UI·오디오·입력·트윈·로그)의 구조와 규칙
- `Assets/_Utility/_Docs/README.md` — 에디터 툴·UI 컴포넌트와 `_Utility` 규칙

두 폴더는 다른 프로젝트에 통째로 복사해 쓰므로, 폴더에 관한 설명은 이 파일이 아니라 각 폴더의 `_Docs`에 둔다. 해당 폴더의 구조·규칙을 바꾸면 그 문서도 함께 고친다.

## 프로젝트 개요

- Unity **6000.3.12f1** (Unity 6.3), **URP 17.3** 프로젝트. PC/Mobile 두 벌의 URP 에셋(`Assets/Settings/PC_RPAsset`, `Mobile_RPAsset`)을 사용한다.
- 입력은 새 Input System(`Assets/InputSystem_Actions.inputactions`)을 사용한다.
- 초기 단계 프로젝트로, 게임 로직은 아직 없다. `_Core`(런타임 프레임워크)와 `_Utility`(에디터 툴/UI 컴포넌트)가 있다.
- 필수 패키지: UniTask, Addressables, Input System, Newtonsoft JSON(`com.unity.nuget.newtonsoft-json`), Vibration(BenoitFreslon).
- git 저장소다(2026-10-28부터, 기본 갈래 `main`). 제외 목록은 루트 `.gitignore`(Unity 생성물·IDE 파일·테스트 스크린샷·`output/`·`SampleImages/`).

## 빌드 / 테스트

빌드·컴파일·테스트는 Unity Editor를 통해 이루어진다. `_Core`는 `Core.asmdef`로 분리된 어셈블리이고, 그 밖의 런타임 코드는 `Assembly-CSharp`, `Editor/` 폴더 하위 코드는 `Assembly-CSharp-Editor`로 컴파일된다(선택 패키지 의존 툴은 하위 폴더별 asmdef — `_Utility/_Docs` 참고).

- 컴파일 확인(에디터 밖): `dotnet build BunnyBunnyDice.sln` — 단, `.csproj`는 Unity가 생성하는 파일이므로 직접 수정하지 말 것. 새 파일 추가 후에는 Unity가 csproj를 재생성해야 반영된다.
- 테스트: `com.unity.test-framework`가 설치되어 있으나 테스트 asmdef/테스트 코드는 아직 없다. 배치 모드 실행 예:
  `"<Unity 경로>/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml [-testFilter <이름>]`
- 코어 점검용 씬: `Assets/_Core/_Test/TestCoreScene.unity`.
- 컴파일 에러는 `Logs/` 또는 Editor.log(`%LOCALAPPDATA%\Unity\Editor\Editor.log`)에서 확인.

## 폴더 구조 규칙

`Assets/` 최상위는 언더스코어 접두 폴더로 역할을 구분한다.

- `_Project/` — 게임 고유 콘텐츠. `_Shared/`(공용, 씬 등), `_Docs/`(문서, 기획서 `GameDesign.md`).
  - **기능별 폴더로 정리한다.** 종류별(`Scripts/`, `Prefabs/`를 최상위에 모으는 방식)로 모으지 않는다.
    기능 폴더 안에 필요한 것만 둔다: `_Project/<기능>/Scripts/`, `_Project/<기능>/Prefabs/`, `_Project/<기능>/Data/`(SO 에셋) 등.
    예: `_Project/Map/Scripts/MapGenerator.cs`, `_Project/Map/Data/SO_MapGenConfig.asset`, `_Project/Card/Prefabs/CardView.prefab`.
- `_Core/` — 프로젝트 간 재사용할 런타임 프레임워크(AbandonedDoll 프로젝트에서 가져옴). `_Docs/README.md` 참고.
- `_Utility/` — 재사용 가능한 도구. 기능별 폴더이고 에디터 코드는 각 기능 폴더 안의 `Editor/`에 둔다(`Tuning/`, `PackageInstaller/`, `UI/` 등). `_Docs/README.md` 참고.
- `_AssetsInternal/`, `_AssetsExternal/` — 자체 제작 에셋 / 외부(구매·다운로드) 에셋. 외부 에셋은 팩 단위 폴더로 둔다(예: `_AssetsExternal/KenneyNatureKit/`).
  - **`Resources/` 폴더는 쓰지 않는다.** 그 안의 에셋은 참조 여부와 무관하게 전부 빌드에 포함된다. 게임 에셋은 Addressables로 불러온다(`_Core`가 자기 프리팹을 두는 `Resources`는 예외).

## 코드 컨벤션

- 네임스페이스를 쓰지 않는다(전역 네임스페이스).
- 클래스명은 역할 접두사를 붙인다: `Window_*`(EditorWindow), `Editor_*`(`[InitializeOnLoad]` 에디터 훅), `SO_*`(ScriptableObject), `UI_Utility_*`(UI 컴포넌트).
- 주석은 한국어로 작성한다. 무엇을 하는지보다 **왜 그렇게 하는지**를 적는다(코드 전반이 이 방식이다).
- LINQ(`System.Linq`)를 쓰지 않는다. for/foreach 루프로 직접 작성한다.
- 게임 코드는 `_Core`를 partial로 확장할 수 없다(별도 어셈블리). 게임 매니저 접근자·입력 상태는 게임 쪽 정적 클래스에 둔다.

### 게임 코드(`_Project`) 설계 규칙

- **기능은 잘게 나눈다.** 나눌 수 있으면 나눈다(데이터 / 규칙·판정 / 표시 / 입력을 한 클래스에 섞지 않는다). 폴더도 기능 단위다(위 폴더 구조 규칙).
- **오브젝트는 모두 동적 생성한다.** 씬에 오브젝트를 미리 배치하지 않고, 코드가 SO·프리팹을 받아 만든다. 예외는 테스트용(검증 씬·미리보기 컴포넌트)뿐이다.
- **설정은 SO로 받는다.** 컴포넌트마다 인스펙터 필드를 늘어놓지 않고, 설정을 담은 `SO_*`를 만들고 코드는 그 SO를 받아 동작한다.
  - **SO = 영구 기본값, GameTuning = 이번 플레이만.** (틀은 `_Utility/Tuning` — `_Utility/_Docs` 참고) 설정 SO는 `SO_Config`를 상속하고, 씬 진입 시 `ConfigTuning.Copy`로 만든 **실행 중 복사본**을 코드에 넘긴다(원본 에셋을 직접 넘기지 않는다).
    - `GameTuning` 오브젝트 인스펙터(복사본)에서 고친 값은 플레이에 바로 적용되고 이번 플레이에서만 유효하다. 원본 SO에는 쓰지 않는다.
    - 원본 SO 에셋을 고치면 영구 저장되고 플레이에도 바로 적용된다 — 그 SO의 복사본은 원본 값으로 통째로 덮인다(GameTuning에서 잠깐 바꿔 둔 값도 원본 값으로 돌아간다).
    - `GameTuning` 인스펙터에는 검색창과, 설정마다 원본 SO 에셋으로 바로 가는 "원본 열기"가 있다.
  - 설정을 쓰는 쪽은 값을 복사해 두지 말고 SO 참조를 들고 매번 읽거나, 복사본의 `Changed`를 구독해 다시 적용한다(`GameScene` 참고).
  - SO·프리팹은 **Addressables**로 불러온다(`Core.Resource.LoadAsync<T>()`, UI는 `Core.UI.Show*<T>()`). 키를 비우면 타입 이름이 키이므로, 씬 진입 시 직접 불러오는 에셋은 **에셋 이름 = 타입 이름 = 주소**로 등록한다(예: `SO_MapGenConfig`). SO가 직접 참조하는 프리팹·하위 SO(카드 등)는 의존성으로 함께 로드되므로 따로 등록하지 않는다.
  - 씬 진입 코드는 씬 이름과 같은 `SceneBase` 하위 클래스(`_Shared/Scenes/Scripts/GameScene.cs`)에 둔다. 씬 파일에는 환경(카메라·조명·볼륨)만 둔다.
- **매직 넘버는 SO로 뺀다.** 밸런스·크기·시간·색 같은 조정값은 코드에 상수로 박지 않는다. 코드에 남겨도 되는 것은 조정 대상이 아닌 값(방향 벡터, 0/1 같은 수학적 상수 등)뿐이다.
- **영웅 특성은 갈고리 인터페이스로 끼어든다.** 시스템은 특성 종류를 모르고, 자기 폴더에 둔 작은 인터페이스(예: `IDicePipRoller`)만 `TraitHooks.Find<T>()`로 묻는다(없으면 원래대로). 특성은 `SO_HeroTrait`를 잇고 필요한 갈고리만 구현한다(`HeroTrait/`). 특성이 자주 생기고 엎어지는 단계라 시스템 코드를 건드리지 않고 더하고 지우게 하려는 것이다.
- **나중에 밸런스를 AI 시뮬레이션으로 맞춘다(기획서 11.2).** 새 코드는 다음을 지킨다 — 기존 코드는 시스템이 거의 다 만들어진 뒤 한 번에 정리한다.
  - 게임 시간은 갱신 함수가 받는 시간 값을 쓴다(`Time.deltaTime`을 곳곳에서 직접 읽지 않는다).
  - 게임 행동(주사위 놓기·카드 고르기·스킬 쓰기·영웅 이동)은 UI를 거치지 않고 코드로 부를 수 있게 둔다(봇이 조작한다).
  - 판 결과에 영향을 주는 난수는 시드로 재현할 수 있게 한다.
  - 측정할 값(피해 출처, 웨이브 정리 시간, 새어 나간 적, 고른 카드)은 이벤트로 내보낸다.
- **화면 글은 번역을 거친다(한국어·영어, `_Core/_Docs` Localization).** 원문(한국어)이 키다. SO의 글 필드는 `[Localized]`를 붙이고 보일 때 `App.Localize`를 부른다. 프리팹의 고정 글은 `UI_LocalizedText`를 붙인다. 코드에 적는 글은 `App.Localize("원문")`처럼 호출 안에 문자열을 바로 적고, 숫자는 `{0}` 자리로 둔다(문자열을 이어 붙이지 않는다 — 어순이 언어마다 다르다). 글을 더하거나 고친 뒤에는 `Tools/Localization/Update Table`로 번역표(`_Shared/Localization/LocalizationTable.csv`)를 맞추고 빈 `en` 칸을 채운다. 테스트 창(F1)·디버그 글은 번역하지 않는다.
  - 언어는 재시작 없이 바뀐다. 코드가 글을 채우는 표시(`CardView`, `UI_GameHud` 등)는 `Core.Localization.OnLanguageChanged`를 OnEnable/OnDisable에서 구독해 자기 글을 다시 채운다.
  - 영어에서 복수형이 생기지 않게 쓴다("Wave 3", "x2"). 폰트(G마켓 산스)에 없는 기호는 Pretendard 대체 폰트가 채운다 — 툴이 둘 다 없는 글자를 알려 준다.
  - **폰트는 두 단계다.** 본문(설명·수치 이름)은 G마켓 산스, 제목(로고·버튼·특성 이름·카드 제목·큰 숫자)은 Black Han Sans(대체: G마켓 → Pretendard). Black Han Sans는 원래 굵어 Bold를 켜지 않는다. 폰트 에셋은 아틀라스 여백 18(두꺼운 외곽선·그림자가 이웃 글자를 읽지 않게) · 2048 · 여러 장 아틀라스 허용 — 한 장이 가득 차면 그 뒤 글자가 대체 폰트로 그려져 낱말 안에서 폰트가 섞인다. 외곽선·그림자 값은 여백(거리 정보 범위)에 대한 비율이라 여백을 바꾸면 머티리얼 값도 같은 비율로 고친다. 머티리얼은 셋 중에 고른다: 기본(폰트 에셋 안), `BlackHanSans Title`(어두운 테두리 + 그림자 — 어두운 바탕·3D 위 글자), `BlackHanSans Logo`(두꺼운 테두리 + 자홍 그림자). 위치는 `_AssetsInternal/Resource/Font/`.
  - **글자 색**: 어두운 바탕 위 제목 = 흰색(0.984, 0.933, 0.988), 강조 숫자·로고 = 노랑(1, 0.88, 0.29), 능력치 이름·소제목 = 청록(0.247, 0.878, 1), 밝은 판(자홍·노랑 버튼·태그) 위 글자 = 먹색(0.1, 0.04, 0.12).
  - **UI 자홍**(테두리·버튼 판·로고 그림자) = (0.961, 0.02, 0.424) — 시안의 쨍한 자홍. 초록이 섞이면 탁해 보이므로 이 값만 쓴다. 게임 안 주사위 자홍(`SO_Dice_Magenta`)은 별개다.
- **카드 글은 짧게, 카드만 봐서는 알 수 없는 것만 적는다.** 무엇을 하는지 + 숨은 규칙·예외(방어력 무시, 보스 절반, 다시 얼지 않는 시간, 돌려받는 것 없음, 눈은 그대로 등)는 적고, 보면 알 수 있는 결과(방어력이 깎이면 더 아프다, 남는 피해는 버려진다)와 쓰임새 힌트는 적지 않는다. "~다"체.
- **카드 글(Description 등)에 숫자를 적지 않는다.** `{필드}`·`{필드%}`·`{1+필드}`·`{1-필드%}` 토큰으로 적으면 `CardText.Fill`이 SO 값으로 채운다(`Card/Scripts/CardText.cs`). 계산으로 나오는 값은 SO가 `ICardTextTokens`로 낸다(예: 범위 스킬의 `{HeroDamage}`). 수치를 고칠 때 글을 따로 고치지 않아도 되게 하려는 것이다.

### UI 배치 규칙

PC·모바일, 가로·세로를 함께 지원한다. Canvas Scaler는 해상도 차이만 흡수하고 화면비 차이는 못 흡수하므로, **모든 UI 위치·크기는 부모 대비 비율로 정한다.**

- **앵커로 영역을 정하고 offset(Left/Top/Right/Bottom, 점 앵커의 Pos/Width/Height)은 0으로 둔다.** `anchorMin ≠ anchorMax`(영역 앵커)가 기본이다. 프리팹을 보면 "부모의 몇 % 영역"인지 바로 읽혀야 한다.
- **가로세로 비율을 지켜야 하는 요소**(카드·아이콘·버튼 등)는 앵커만으로 찌그러지므로 `UI_Utility_FlexLayout`(`Aspect`/`Percent`/`Fill`) 또는 `AspectRatioFitter`가 크기를 정한다. 한 줄로 늘어놓는 배치는 FlexLayout을 쓴다.
- **함께 붙어 있어야 하는 묶음**(제목 + 카드 줄 등)은 화면 영역에 각자 앵커를 걸지 않는다. 화면비가 바뀌면 서로 멀어지거나 겹친다. 여백용 영역(앵커) 안에 `AspectRatioFitter`(Fit In Parent) 묶음을 두고, 그 안에서 앵커로 나눈다 — 묶음은 가운데 놓인 채 통째로 줄어든다(`UI_LevelUpPopup` 프리팹 참고).
- **픽셀 단위 레이아웃 값을 쓰지 않는다.** `Horizontal/Vertical/GridLayoutGroup`의 spacing·cell size, FlexLayout의 `padding`(RectOffset, 픽셀)은 쓰지 않는다(padding은 0, 안쪽 여백은 자식 앵커로). 간격은 FlexLayout의 `spacingRatio`.
- **텍스트는 영역에 맞춘다.** TMP는 Auto Size(min/max)를 켠다 — 영역이 비율로 바뀌는데 글자 크기가 고정이면 넘치거나 남는다.
- **배치 비율은 프리팹 앵커에 둔다(SO로 빼지 않는다).** "매직 넘버는 SO로" 규칙은 게임 수치·연출 값에 적용하고, UI 영역 비율은 프리팹이 데이터다.
- **코드가 만드는 요소**(카드 등)도 코드가 픽셀 좌표를 계산하지 않는다. 앵커로 정한 영역 안에 FlexLayout이 빈 **슬롯**을 배치하고, 요소는 슬롯의 자식이 되어 슬롯 크기에 맞춘다. 레이아웃이 요소를 직접 움직이면 요소의 목표 따라가기·트윈과 부딪힌다(`UI_LevelUpPopup` 참고).
- **예외**
  - 실행 중 움직임(날아가기·팝 연출·드래그)은 offset·scale을 써도 된다 — 쉬는 자리가 비율이면 된다.
  - 슬롯으로 표현하기 어려운 배치(손패 부채꼴 등)는 코드가 계산하되, SO의 거리 값은 픽셀이 아니라 **화면 비율에서 나온 단위의 배수**로 둔다(손패: 카드 높이 = 화면 짧은 변 × 비율, 거리는 카드 높이 배수 — `CardSizing` 참고). 화면이 바뀌면 다시 계산한다(`UI_GameHud.Resized`).
  - 글자 길이로 폭이 정해지는 요소(카드의 강함/약함 태그)는 가로만 코드가 계산한다. 세로는 앵커로 둔다.
  - 1~2px 선·9-slice 테두리 두께, 최소 터치 크기처럼 화면에 따라 변하면 안 되는 값.

## 주의 사항

- `Debug.Log`는 `_Core/Debug.cs` 래퍼에 잡혀 `DEBUG_ENABLE` 심볼이 없으면 호출째 제거된다. 현재 `DEBUG_ENABLE`은 Standalone에만 정의되어 있다(다른 플랫폼은 Custom Symbols 툴로 추가).
- `EditorBuildSettings`의 씬 경로가 `Assets/Scenes/SampleScene.unity`로 남아 있으나 실제 씬은 `Assets/_Project/_Shared/Scenes/SampleScene.unity`로 이동되어 있다(GUID로 추적되긴 함).
- 에셋을 이동/이름 변경할 때는 `.meta` 파일을 함께 옮겨야 GUID 참조가 유지된다. `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`은 Unity 생성물이다.

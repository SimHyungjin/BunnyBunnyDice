# _Core

프로젝트 간 재사용하는 런타임 프레임워크. 이 폴더(문서 포함)는 다른 프로젝트에 통째로 복사해 쓴다.
원본은 AbandonedDoll 프로젝트의 `_Core`이며, Def로 가져오며 서비스 레이어를 빼고 전 모듈을 재검토·재작성했다.

## 어셈블리 · 의존

- `Core.asmdef`로 분리된 어셈블리다. 참조: UniTask, UniTask.Addressables(Addressables 핸들 await 확장), Unity.Addressables, Unity.ResourceManager, Unity.InputSystem, UnityEngine.UI, Unity.TextMeshPro, Vibration. Newtonsoft JSON(`com.unity.nuget.newtonsoft-json`)은 자동 참조된다.
- Core는 게임 코드(`Assembly-CSharp`)를 참조할 수 없고, 게임 쪽에서 Core 클래스를 partial로 확장할 수 없다.
  - 게임 매니저 접근자는 게임의 정적 클래스에 둔다. 예: `public static BattleManager Battle => Core.Get<BattleManager>();`
  - 게임 입력 상태는 게임 소유 정적 클래스에 두고 `Core.Input.OnInputsReset`을 구독해 초기화한다(`ButtonInput` 주석 참고).
- `USE_VIBRATION`은 Vibration 패키지가 설치돼 있으면 asmdef `versionDefines`가 Core 안에서 자동으로 켠다.

## 골격

- **컴포지션 루트 `Core`(`Core.cs`)** 가 유일한 MonoBehaviour 싱글턴이다. `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`로 어느 씬에서 플레이하든 자동 부팅되고(`@Core`), Unity 콜백(Update/Pause/Quit)을 받아 매니저에 전달한다.
- **매니저는 순수 C# 클래스**(`Manager` 상속)다. Update를 직접 갖지 않고 `Core.Loop`의 이벤트를 구독한다. 초기화 순서는 각 매니저의 `Dependencies` 선언으로 정해지고 `ManagerRegistry`가 위상 정렬해 같은 계층은 병렬 초기화한다. 정리(`Clear`)는 초기화의 역순이다. 초기화·정리 실패는 로그만 남기고 계속한다.
- **부팅**: Core 스테이지 매니저 → `BeforeService` 게이트 → Service 스테이지 매니저 → `AfterService` 게이트 → `IsReady` → 현재 씬 진입. 게이트(`IBootGate`)가 `Blocked`면 진입하지 않는다(`Core.RetryBootAsync`로 재시도).
- **부팅은 비동기라 첫 씬의 Awake/Start가 매니저 초기화보다 먼저 돌 수 있다.** 씬에는 로직을 두지 않는 것이 전제다 — 씬 로직은 `SceneBase`에서 시작하거나 `await Core.WaitUntilReady()` 이후에 매니저를 쓴다. 에디터에서는 초기화 전 접근을 `Core.Resolve`가 경고한다(Dependencies 누락도 잡힌다).
- **접근**: 코어 매니저는 `Core.Xxx`(예: `Core.UI`), 자주 쓰는 동작은 정적 파사드 `App`. 게임 매니저는 `Core.Get<T>()`.
- **확장은 `Core.cs`를 고치지 않고 `ServiceProviders`로 한다.** `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`에서 등록한다.
  - `Override<T>(...)` — 기본 구현 **교체**(마지막 등록이 이김). 예: `Override<IHapticProvider>`
  - `Add<T>(...)` — 목록에 **추가**(누적). 예: `Add<Manager>`(게임·서비스 매니저), `Add<IBootGate>`(게이트). Core가 부팅 때 `TakeAll<T>()`로 가져가면서 비운다(도메인 리로드 끔 대응).
- **서비스 레이어(광고·결제·분석·인증·클라우드 세이브·동의·원격 설정 등)는 의도적으로 제외했다.** 원본은 AbandonedDoll `_Core/Service/`. 다시 붙일 때는 서비스 매니저(Stage = Service)와 게이트를 `ServiceProviders`로 등록하고, 서비스가 저장할 값은 서비스 쪽 `SaveSection`으로 정의한다. 쓰지 않는 SDK가 빌드에 포함되지 않도록 서비스/SDK 구현체별 asmdef(`defineConstraints`)로 분리한다.
- 설정값은 `SO_CoreConfig`(`Config/Resources/`)에 모은다. 코어가 `Resources`에서 로드하는 에셋은 `CoreCanvas`·`CoreEventSystem`·`CoreAudioMixer`처럼 코어 소유가 드러나는 이름을 쓴다(Resources는 프로젝트 전체가 한 이름 공간).

## Loop · AppState

- `LoopManager`의 `OnUpdate`(unscaled) / `OnGameUpdate`(플레이어 배율) / `OnLateUpdate`. 구독자는 하나씩 예외 격리되어 불리고, 구독·해제에 할당이 없다. `WorldTimeScale`은 `Time.timeScale` 그 자체다.
- 구독자마다 프로파일러 표식(`타입.메서드`, `ENABLE_PROFILER` — 에디터·개발 빌드)이 붙는다. 프로파일러에서 Core.Update 아래로 시스템별 시간이 보인다.
- `LoopMonoBehaviour`를 상속하면 override한 갱신 메서드의 채널만 구독한다.
- `AppStateManager`: 포그라운드/백그라운드 전환(`OnEnterForeground`/`OnEnterBackground`), 억제와 무관한 저장용 `OnBackgroundRaw`(전환당 1회). 광고·결제창 동안의 전환 억제는 `using (Core.AppState.SuppressStateEvents()) { ... }`(카운터·스코프, 겹쳐도 안전, 무시가 아니라 연기).

## Scene

- `SceneFlowManager`가 씬 이름과 같은 이름의 `SceneBase` 하위 클래스를 **모든 어셈블리**에서 찾아 실행한다. 네임스페이스 없이 두고, 씬 이름은 클래스 이름 규칙을 따라야 한다. 없으면 에디터에서 경고.
- **전환 순서**: 전환 연출(`onBeforeLoad`, 이전 씬은 아직 살아 있음) → `ExitScene` → 씬 토큰 취소 + `Core.Clear()`(씬 에셋 스코프도 이때 닫힘 — 이전 씬 작업이 한 시점에 끊긴다) → additive 로드 → 이전 씬 언로드 → `onAfterLoad` → `EnterScene`. 로드할 수 없는 씬 이름은 정리 전에 거부한다. 리로드는 같은 이름으로의 전환이다.

## Resource · Pool

- 에셋은 `AssetScope` 단위로 소유한다. 스코프로 로드한 에셋은 스코프가 Dispose될 때 해제되고(한 프레임 뒤), 참조 카운트는 Addressables에 맡긴다. 코어 스코프는 `Core.Resource.Scene`(씬 전환 시 교체)과 `Core.Resource.Persistent`이며 `AssetLifetime`으로 고른다. 별도 수명은 `Core.Resource.CreateScope`.
- 캐시 키는 (주소, 로드 타입)이라 `LoadAsync<Sprite>`가 동작한다. 컴포넌트 타입을 요청하면 프리팹(GameObject)으로 로드해 꺼내 준다.
- 스코프는 자기 수명 토큰(`Token`)을 가진다. 그 스코프의 로드·스폰 대기는 스코프가 닫힐 때 자동 취소된다(매니저끼리 서로의 토큰을 받아 쓰지 않는다 — 취소 시점은 소유자의 수명이 정한다).
- 풀(`PoolManager`)은 GameObject 단위이고 프리팹을 로드한 스코프에 속한다(스코프가 닫히면 그 풀과 활성 인스턴스까지 정리). 풀 루트가 비활성이라 예열 시 Awake/OnEnable이 돌지 않는다.
- 위치·회전을 넘기는 스폰은 **월드 좌표**, 부모만 넘기는 스폰은 **로컬 원점**. `IPoolable`/`IAfterSpawn`은 자식 컴포넌트까지 불린다. 예약 반납은 `ScheduleDespawn`(세대 번호로 재사용 시 자동 무효화). `ParticleAutoDespawn`은 자식 파티클의 지연+기간+수명으로 반납 시점을 계산한다.

## Data (세이브)

- **섹션 단위다.** `SaveSection`을 상속한 클래스 하나가 파일 하나(`persistentDataPath/Save/{Key}.sav`)이고, 코어(`SettingsData`)와 게임이 각자 정의한다 — 코어 파일에 게임 필드를 넣지 않는다.
- `Core.Data.Get<T>()`로 가져와(항상 같은 인스턴스) 값을 바꾼 뒤 `section.MarkDirty()`. 저장은 바뀐 섹션만, 백그라운드 전환·종료(동기)·씬 전환·자동 저장(`SO_CoreConfig.DataAutoSaveInterval`)에서 한다.
- 기본 형식은 Newtonsoft JSON(public 필드만, Dictionary 가능). 크거나 자주 저장되는 섹션은 `IBinarySection`으로 바이트를 직접 쓴다. 출시 후 바뀔 수 있는 클래스는 `Key`를 고정 문자열로 override한다. 스키마 변환이 필요하면 `Version`을 올리고 `Migrate`를 구현한다. 스트리핑을 High로 올리면 섹션 클래스에 `[Preserve]`가 필요할 수 있다.
- 파일은 원자적으로 쓰고(.tmp → .bak 교체), CRC32로 검증하며, 깨지면 .tmp/.bak에서 복구하고 실패 시 .corrupt로 보존한다(조용히 덮어쓰지 않는다). 빌드에서는 GZip + XOR 난독화, 에디터에서는 평문(보안 목적 아님).
- 클라우드용 `ExportAll`/`ImportAll`, `ExportSection`/`ImportSection`(바이트, 검증 실패 시 미적용), `GetModifiedUtc`. 가져오기·초기화 후 `OnDataReplaced`가 발생한다(섹션 인스턴스는 그대로, 내용만 바뀜).

## UI

- 레이어는 아래부터 **World**(월드 위 UI, 여러 개) · **Hud**(하나) · **Popup**(스택) · **Transition**(전환 커튼, 하나). UI 프리팹은 Addressables 키(기본: 타입 이름)로 로드되어 풀링된다.
- `Show*`에 `AssetLifetime`을 넘긴다. 씬 전환(`UIManager.Clear`) 때 Scene 수명 UI는 즉시 닫히고 Persistent는 남는다(Transition 기본값은 Persistent).
- `UI_Panel`의 `OnOpenEvent`/`OnCloseEvent`는 **표시 단위** 구독이라 반납 시 지워진다. 매번 해야 하는 처리는 `Open`/`Close` override나 `OnDespawned`에 둔다.
- 연출(`UI_Animation`)은 프리팹에 저작된 위치·크기(`RestAnchoredPosition`/`RestScale`)를 기준으로 하며, `PlayIn`/`PlayOut`은 끝까지 재생됐는지(bool)를 돌려준다. 팝업은 닫기 연출이 끊기면 닫기 처리를 하지 않는다.
- **뒤로가기**: 코어 입력 모듈 `CoreBackInput`(ESC·Android 뒤로가기, 부팅 때 켜짐, `SO_CoreConfig.EnableBackInput`)이 `App.HandleBackPressed()`로 보낸다 → 맨 위 팝업(`CloseOnBack`이 꺼져 있으면 닫지 않고 소비) → 없으면 `AppState.OnBackPressed`. 팝업마다 등록할 것은 없다.

## Audio

- 에셋 단위다: 효과음은 `SO_SoundCue`(클립 무작위·볼륨·피치 범위·채널·3D·동시 재생 수/최소 간격 제한·루프), 배경음은 `SO_MusicTrack`(인트로+본체, `PlayScheduled`로 샘플 단위 이음). **중앙 표·enum·코드 생성은 없다.**
  - 오브젝트는 큐를 `[SerializeField]`로 직접 참조한다.
  - 코드가 이름으로 부르는 소리(UI 클릭 등)는 게임 쪽에 큐를 필드로 모은 SO를 둔다(오타는 컴파일 에러, 순서 무관).
- 볼륨은 `Resources/CoreAudioMixer`(그룹 `Music`/`SFX`, 노출 파라미터 `MasterVolume`/`MusicVolume`/`SfxVolume`)로 다룬다. 믹서가 없으면 소스 볼륨을 곱하는 방식으로 대신한다.
- 효과음 소스 수는 고정(`SfxPoolSize`)이고 모자라면 덜 중요·오래된 소리를 빼앗는다. 재생 결과는 `SoundHandle`(루프 정지·재생 여부).
- 씬 전환 시 효과음만 멈추고 배경음은 유지된다. 배경음 페이드는 매 프레임 직접 진행한다(비동기 경합 없음).
- 볼륨 **저장은 `SettingsManager`만** 한다(`App.SetVolume`이 Settings를 거침) — AudioManager는 값을 적용만 하고 Data를 모른다.

## Localization (번역)

- **원문이 곧 키다.** 게임은 SO·프리팹·코드에 원문(한국어, `SO_CoreConfig.SourceLanguage`)을 그대로 적고, 보일 때 `App.Localize(원문)`을 거친다. 번역이 없으면 원문이 보인다. 키를 따로 짓지 않으므로 원문을 고치면 그 줄은 새 줄이 되고(낡은 번역이 남지 않는다), 같은 원문은 어디서든 같은 번역이다.
- **번역표**는 CSV 하나(Addressables 주소 `SO_CoreConfig.LocalizationTableKey`, 기본 `LocalizationTable`). 첫 열 = 원문 언어, 나머지 열 = 언어 코드(`en`, `ja`…), `#`로 시작하는 열(`#where` — 쓰이는 곳)은 사람용이라 읽지 않는다. 표가 없으면 원문만 보인다.
- **원문을 모으는 곳 세 군데** — `Tools/Localization/Update Table`이 모아 표를 맞춘다(새 원문 추가, 안 쓰는 줄 삭제 — 지운 줄은 번역과 함께 로그에 남는다, Addressables 등록. 결과 요약은 대화창 없이 콘솔에만 남긴다 — 대화창은 닫을 때까지 에디터를 멈춰 자동화가 끊긴다):
  - SO의 `[Localized]` string 필드(중첩 구조체·리스트 안까지). 표시하는 쪽이 `App.Localize(field)`를 부른다.
  - 프리팹 TMP에 붙인 `UI_LocalizedText` — 프리팹에 적힌 글이 원문. 코드가 채우는 글에는 붙이지 않는다(언어가 바뀌면 원문으로 되돌린다).
  - 코드의 `App.Localize("원문")` — **문자열을 호출 안에 바로** 적은 것만 찾는다(변수·배열은 못 찾는다). 숫자가 들어가면 `App.Localize("웨이브 {0}", wave)`처럼 자리를 남긴다(어순이 언어마다 다르다).
- 툴이 함께 검사하는 것: 언어별 빈 번역 수, 원문과 번역의 `{토큰}`이 다른 줄, 프리팹이 쓰는 폰트(대체 폰트 포함)에 없는 글자.
- 언어 선택은 `Core.Settings.Language`(빈 값 = 기기 언어, `SettingsData`에 저장). 표에 없는 언어면 `FallbackLanguage`, 그것도 없으면 원문 언어. 바뀌면 `Core.Localization.OnLanguageChanged` — 글을 직접 채우는 표시는 이것을 구독하거나 다시 그린다.
- 에디터 툴은 `_Core/Editor/`(`Core.Editor.asmdef`, Editor 전용)에 있다.

## Input · 기타 매니저

- 입력 모듈(`InputActions`)은 켜진 순서대로 갱신되고(모듈별 예외 격리), 씬 전환 시 꺼지고 버려진다. 앱 전체에서 늘 필요한 모듈은 `Persistent => true`로 두면 유지되며 `SetInput`/`RemoveAllInputs`의 영향도 받지 않는다(끄려면 `RemoveInput`).
- `InputManager.ScreenToPlane`은 원근·정사영 모두에서 화면 좌표 → 평면 위 점을 구한다(`ScreenToWorld(pos)`는 2D용).
- `SettingsManager`: 볼륨·진동·프레임레이트·수직동기·해상도의 저장/복원. 값이 같으면 저장 대상으로 표시하지 않는다.
- `HapticManager`: 구현은 `IHapticProvider`(Null/Log/Mobile, `ServiceProviders.Override`로 교체). 지원 여부는 구현이 `IsSupported`로 알린다.
- `SafeAreaManager` + `SafeArea` 컴포넌트: 짧은 축 인셋 무시 여부는 `SO_CoreConfig.SafeAreaIgnoreMinorAxis`.
- `CameraManager`(추적·셰이크·수동 배치), `DisplayManager`(프레임레이트·수직동기·해상도·커서), `DeepLinkManager`(호스트별 처리기, 처리기 등록 전 링크는 보관).

## Tween

- `CoreTween`: 외부 트윈 의존 없이 코어가 쓰는 최소 트윈(스케일·로컬 위치·anchoredPosition·알파·오디오 볼륨). 게임 쪽은 PrimeTween 등을 써도 된다.
- `await tween`은 완료면 true, 중단(Stop·대상 파괴·같은 대상·속성의 새 트윈)이면 false. 같은 대상의 같은 속성에 새 트윈을 걸면 기존 것은 중단된다.

## 로그

- **전역 `Debug` 래퍼(`Debug.cs`)** 가 `UnityEngine.Debug`를 가린다(Core 어셈블리에 있어도 게임 코드의 `Debug.Log`를 잡는다). `Debug.Log`/`LogFormat`/`Assert`/`Draw*`/`Break`는 `DEBUG_ENABLE` 심볼이 없으면 `[Conditional]`로 호출째 제거된다. 경고·에러·예외는 유지된다.
- 출시 빌드(`DEBUG_ENABLE` 없음, 에디터 아님)에서는 `unityLogger.filterLogType = Warning`으로 래퍼가 닿지 않는 코드(asmdef 에셋·DLL)의 일반 로그 출력도 막는다.
- 래퍼는 `UnityEngine.Debug`의 자주 쓰는 API를 갖춰야 한다 — 없는 멤버를 쓰는 외부 에셋 스크립트는 컴파일 에러가 난다.

## 점검

- `_Test/TestCoreScene.unity`는 코어 점검용 씬이다(Addressables에 `UI_TestPopup` 주소 등록 필요).

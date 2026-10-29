/// <summary>
/// 판 흐름(GameScene)이 읽는 테스트 상태. 켜는 곳은 테스트 키·창(GameScene.Debug)과 색 시험뿐이라 출시 빌드에서는 늘 꺼져 있다.
/// 규칙 클래스(PlayerLevel·GoalHealth)가 아니라 여기 두는 이유: 테스트 상태가 게임 규칙에 섞이지 않게 — 연결(GameScene)만 막는다.
/// </summary>
public sealed class GameDebugFlags
{
    /// <summary>경험치 막기 — 레벨업 팝업 없이 웨이브·스킬을 시험할 때. 보스 보상 고르기도 막는다.</summary>
    public bool ExpBlocked;
    /// <summary>무적 — 목표물이 피해를 받지 않는다(받았을 피해는 측정 기록에 센다).</summary>
    public bool GoalInvincible;
}

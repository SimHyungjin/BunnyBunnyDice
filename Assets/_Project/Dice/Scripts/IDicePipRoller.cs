/// <summary>
/// 갈고리: 타워 주사위의 눈 굴림을 바꾼다(놓기·얹기·리롤·올인). 판 시작 주사위(칩 수를 정하는 굴림)는 맵 생성이라 지나지 않는다.
/// 특성 등 바깥에서 구현한다(SO_HeroTrait).
/// </summary>
public interface IDicePipRoller
{
    /// <param name="placing">손패에서 놓거나 얹는 굴림이면 true, 스킬로 다시 굴리는 것(리롤·올인 등)이면 false — 777은 놓을 때만 7이다.</param>
    /// <returns>1 ~ faces 사이의 눈.</returns>
    int RollPips(int faces, bool placing);
}

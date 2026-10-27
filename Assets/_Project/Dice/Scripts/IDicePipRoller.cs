/// <summary>
/// 갈고리: 타워 주사위의 눈 굴림을 바꾼다(놓기·얹기·다시 굴리기·올인). 판 시작 주사위(칩 수를 정하는 굴림)는 맵 생성이라 지나지 않는다.
/// 특성 등 바깥에서 구현한다(SO_HeroTrait).
/// </summary>
public interface IDicePipRoller
{
    /// <returns>1 ~ faces 사이의 눈.</returns>
    int RollPips(int faces);
}

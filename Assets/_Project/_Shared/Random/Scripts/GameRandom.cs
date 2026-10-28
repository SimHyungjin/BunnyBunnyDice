/// <summary>
/// 판 결과에 영향을 주는 난수(기획서 11.2 — 시뮬레이션으로 밸런스를 맞추려면 같은 시드 + 같은 선택 = 같은 판이어야 한다).
/// 판을 시작할 때 맵 시드로 Begin한다. 맵·칩은 이미 그 시드로 만들어지므로, 판 하나가 시드 하나로 통째로 정해진다.
///
/// 줄 하나하나는 코어의 RandomStream이고, 여기는 이 게임의 시스템 목록만 둔다(어떤 줄이 있는지는 게임 규칙이라).
/// 시스템마다 줄을 나누는 이유: 한 줄을 같이 쓰면 치명타를 한 번 더 굴리는 것만으로 카드 후보·웨이브 특성이 밀려
/// 카드 하나를 바꾼 비교가 맵 운까지 바꾼다. 줄이 따로면 바꾼 시스템만 달라진다.
/// 연출(흔들림·구르는 축·피해 숫자 위치)은 여기를 쓰지 않고 UnityEngine.Random을 쓴다 — 표시를 고쳐도 판 결과가 그대로이게.
/// 메인 스레드에서만 쓴다(맵 생성 워커는 자기 System.Random을 쓴다).
/// </summary>
public static class GameRandom
{
    /// <summary>주사위 눈 굴림·검정 각성 색·눈 고르기 특성.</summary>
    public static readonly RandomStream Dice = new RandomStream(0);
    /// <summary>치명타(주사위·영웅·스킬).</summary>
    public static readonly RandomStream Crit = new RandomStream(0);
    /// <summary>레벨업 후보·손패 넘침 버리기·카드 특성.</summary>
    public static readonly RandomStream Cards = new RandomStream(0);
    /// <summary>경험치 특성.</summary>
    public static readonly RandomStream Exp = new RandomStream(0);
    /// <summary>스킬 효과(흩뿌리기 자리).</summary>
    public static readonly RandomStream Skill = new RandomStream(0);
    /// <summary>웨이브 구성(무작위 특성)·잭팟.</summary>
    public static readonly RandomStream Wave = new RandomStream(0);

    static GameRandom() => Begin(System.Environment.TickCount);

    /// <summary>모든 줄을 seed에서 다시 시작한다. 줄 번호는 바꾸지 않는다 — 바꾸면 같은 시드의 예전 기록과 판이 달라진다.</summary>
    public static void Begin(int seed)
    {
        Dice.Reset(RandomStream.DeriveSeed(seed, 1));
        Crit.Reset(RandomStream.DeriveSeed(seed, 2));
        Cards.Reset(RandomStream.DeriveSeed(seed, 3));
        Exp.Reset(RandomStream.DeriveSeed(seed, 4));
        Skill.Reset(RandomStream.DeriveSeed(seed, 5));
        Wave.Reset(RandomStream.DeriveSeed(seed, 6));
    }
}

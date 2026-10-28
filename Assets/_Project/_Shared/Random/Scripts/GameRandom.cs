/// <summary>
/// 판 결과에 영향을 주는 난수(기획서 11.2 — 시뮬레이션으로 밸런스를 맞추려면 같은 시드 + 같은 선택 = 같은 판이어야 한다).
/// 판을 시작할 때 맵 시드로 Begin한다. 맵·칩은 이미 그 시드로 만들어지므로, 판 하나가 시드 하나로 통째로 정해진다.
///
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

    /// <summary>모든 줄을 seed에서 다시 시작한다. 줄마다 다른 시드를 섞어 내 같은 수열이 되지 않게 한다.</summary>
    public static void Begin(int seed)
    {
        Dice.Reset(Mix(seed, 1));
        Crit.Reset(Mix(seed, 2));
        Cards.Reset(Mix(seed, 3));
        Exp.Reset(Mix(seed, 4));
        Skill.Reset(Mix(seed, 5));
        Wave.Reset(Mix(seed, 6));
    }

    // splitmix64 한 걸음 — 이웃한 시드(맵 시드는 TickCount라 붙어 있다)와 줄 번호에서 서로 동떨어진 시드를 낸다.
    // System.Random은 이웃한 시드의 첫 값들이 비슷해 그대로 넣으면 줄끼리·판끼리 닮는다.
    private static int Mix(int seed, int stream)
    {
        unchecked
        {
            ulong z = (ulong)(uint)seed + (ulong)stream * 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (int)z;
        }
    }
}

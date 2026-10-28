using System.Collections.Generic;
using UnityEngine;

public enum TileType
{
    Ground, // 몬스터가 지나가는 바닥. 주사위·칩을 놓으면 길이 막힌다(미로 설계).
    Goal,   // 중앙 목표물. 몬스터의 도착점이라 통과 가능으로 취급한다.
    Spawn,  // 적 출현 구역(열린 포켓 앞 칸). 길은 되지만 주사위는 못 놓는다(출구를 막아버리는 배치 방지).
    Wall,   // 길도 아니고 주사위도 못 놓는 칸. 지금 생성기는 만들지 않는다(둘레 벽을 없애고 맵 밖을 경계 검사로 막는다) — 판 안 벽이 필요해지면 쓴다.
}

/// <summary>
/// 한 판의 맵 그리드. 좌표는 (x, y) = (열, 행)이고 월드에서는 (x, 0, y)에 대응한다.
/// 생성(MapGenerator)·경로(MapPathfinder)·표시(MapPreview)가 모두 이 데이터만 보고 동작하도록
/// 렌더링이나 MonoBehaviour와 분리해 둔다.
/// </summary>
public sealed class MapData
{
    public readonly int Width;
    public readonly int Height;
    /// <summary>포켓(당구대 구멍) 앞 칸 6곳: 네 모서리 + 긴 변 가운데 둘. 순서가 포켓 번호(1~6, 주사위 눈)다.</summary>
    public readonly List<Vector2Int> Pockets = new List<Vector2Int>();
    /// <summary>이번 판 주사위 굴림 결과(눈). 나온 눈의 포켓이 열리고, 합이 시작 칩 수다(기획서 3.3).</summary>
    public readonly List<int> Roll = new List<int>();
    /// <summary>이번 판에 적이 나오는 포켓 칸(Pockets의 일부, 번호 순, 겹치지 않음).</summary>
    public readonly List<Vector2Int> Spawns = new List<Vector2Int>();
    /// <summary>적을 내보내는 차례. 주사위 하나당 한 칸이라 같은 눈이 겹친 포켓은 그만큼 자주 나온다(돌아가며 섞여 있다).</summary>
    public readonly List<Vector2Int> SpawnGates = new List<Vector2Int>();
    /// <summary>
    /// 보스가 나오는 포켓(기획서 11.3). 판 주사위가 가장 많이 들어간 포켓이고, 같으면 그중 무작위다. 판 시작 때 정해 내내 같다
    /// — 보스가 지날 길을 미리 알고 미로를 짤 수 있게.
    /// </summary>
    public Vector2Int BossPocket = new Vector2Int(-1, -1);
    public RectInt GoalRect;
    /// <summary>
    /// 맵 생성 검사(기획서 3.3)가 이 판에 지어 본 미로 — 주사위를 놓을 칸들. 검사는 "이 미로로 풀린다"고 보고 판을 통과시켰으니
    /// 봇의 첫 계획도 이것이다(MazePlanner). 검사가 없었으면 비어 있다.
    /// </summary>
    public readonly List<Vector2Int> MazePlan = new List<Vector2Int>();

    private readonly TileType[] _tiles;
    // 지형(TileType)과 따로 둔다 — 주사위·칩은 판 중에 놓이지만 지형은 생성 후 고정이다.
    private readonly bool[] _dice;
    // 칩(칩 카드, 기획서 3.4)이 놓인 칸. 칩 종류는 ChipField가 든다 — 맵은 길을 막는지만 안다.
    private readonly bool[] _chips;

    public MapData(int width, int height)
    {
        Width = width;
        Height = height;
        _tiles = new TileType[width * height];
        _dice = new bool[width * height];
        _chips = new bool[width * height];
    }

    /// <summary>지금 모습 그대로의 사본. 워커 스레드가 판 중에 바뀌는 맵을 읽지 않게 넘길 때 쓴다(MazePlanner).</summary>
    public MapData Clone()
    {
        MapData copy = new MapData(Width, Height);
        copy.Pockets.AddRange(Pockets);
        copy.Roll.AddRange(Roll);
        copy.Spawns.AddRange(Spawns);
        copy.SpawnGates.AddRange(SpawnGates);
        copy.BossPocket = BossPocket;
        copy.GoalRect = GoalRect;
        copy.MazePlan.AddRange(MazePlan);
        System.Array.Copy(_tiles, copy._tiles, _tiles.Length);
        System.Array.Copy(_dice, copy._dice, _dice.Length);
        System.Array.Copy(_chips, copy._chips, _chips.Length);
        return copy;
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public int ToIndex(int x, int y) => y * Width + x;

    public TileType Get(int x, int y) => _tiles[ToIndex(x, y)];

    public void Set(int x, int y, TileType type) => _tiles[ToIndex(x, y)] = type;

    public bool HasDice(int x, int y) => _dice[ToIndex(x, y)];

    public void SetDice(int x, int y, bool value) => _dice[ToIndex(x, y)] = value;

    public bool HasChip(int x, int y) => _chips[ToIndex(x, y)];

    public void SetChip(int x, int y, bool value) => _chips[ToIndex(x, y)] = value;

    /// <summary>주사위·칩을 놓을 수 있는 지형인지(점유 여부는 따로 본다).</summary>
    public bool IsBuildable(int x, int y) => Get(x, y) == TileType.Ground;

    // 주사위와 칩은 모두 벽이다. 칩 위에 놓는 주사위는 이미 막힌 칸이라 길에 영향이 없다.
    public bool IsWalkable(int x, int y) => Get(x, y) != TileType.Wall && !HasDice(x, y) && !HasChip(x, y);
}

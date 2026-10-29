using UnityEngine;

/// <summary>맵 생성 규칙(크기·목표물·출현 구역·시작 칩). MapGenerator가 이 값만 보고 맵을 만든다.</summary>
[CreateAssetMenu(menuName = "Def/Map/Map Gen Config", fileName = nameof(SO_MapGenConfig))]
public sealed class SO_MapGenConfig : SO_Config
{
    [Header("크기")]
    [Tooltip("맵(플레이 영역) 크기.")]
    [Min(5)] public int Width = 15;
    [Min(5)] public int Height = 8;
    public Vector2Int GoalSize = new Vector2Int(2, 2);

    [Header("판 주사위 (출현 구역·시작 칩)")]
    [Tooltip("판 시작 때 굴리는 d6 수. 나온 눈의 포켓이 열리고(겹치면 그 포켓에서 그만큼 더 나온다), 눈의 합이 시작 칩 수다. "
        + "3개면 열리는 포켓 평균 약 2.5곳(4곳은 없다), 칩 평균 10.5개(3~18). 4개였을 때는 4곳 열리는 판(28%)이 미로를 짓기 어려웠고 칩이 장애물로 느껴졌다(2026-10-29).")]
    [Range(1, 6)] public int SpawnDiceCount = 3;
    [Tooltip("다시 하는 최대 횟수. 합만큼 칩을 놓지 못하면 주사위부터, 미로 검사를 못 넘으면 칩만 다시 한다. "
        + "다 쓰면 미로 검사에서 가장 나았던 맵(그것도 없으면 마지막 결과 — 합과 칸 수가 달라진다)을 쓴다.")]
    [Min(0)] public int MaxRerolls = 30;
    [Tooltip("미로를 잘 지어도(MazeRouteProbe, 보스·×2 구멍을 더 쳐 주며 짓는다) 가장 짧은 구멍의 길이 이보다 짧으면 칩만 다시 놓는다(칸, 0 = 검사 안 함). "
        + "주사위가 모자라 한 구멍을 희생해야 하는 맵이 판 결과를 크게 갈랐다(기획서 3.3).")]
    [Min(0f)] public float MinShortestRoute = 16f;
    [Tooltip("칩 배치 하나에 미로를 지어 보는 최대 횟수. 한 번이라도 기준을 넘으면 통과다 — 좋은 미로가 여럿이라 한 번 지은 결과는 크게 흔들린다.")]
    [Min(1)] public int ProbeTries = 5;

    [Header("시작 칩 (자리 — 종류는 ChipField가 칩 카드 가중치로 뽑는다)")]
    [Tooltip("칩 하나를 놓을 때 뽑는 후보 자리 수. 그중 이미 놓인 칩에서 가장 먼 곳에 놓는다 — 클수록 고르게 퍼지고, 1이면 그냥 무작위(덩어리·쏠림이 생긴다).")]
    [Min(1)] public int ChipSpreadCandidates = 10;
    [Tooltip("칩 무리 크기(최소, 최대). 무리의 첫 칩만 고르게 퍼뜨리고 나머지는 그 옆에 붙인다. 1~1이면 낱개(고르지만 심심하다), "
        + "무리끼리는 닿지 않게 해 큰 덩어리가 되지 않는다(이전 줄 배치는 서로 엉겨 10개 중 8개가 붙었다).")]
    public Vector2Int ChipGroupSize = new Vector2Int(1, 3);
    [Tooltip("목표물 둘레 이 거리 안에는 칩을 두지 않는다. 0이면 목표물 바로 옆이 막혀 입구가 한 줄로 정해지기 쉽다. 넓을수록 놓을 자리가 줄어 칩이 반대쪽으로 쏠린다.")]
    [Min(0)] public int GoalClearance = 1;
    [Tooltip("출현 구역 둘레 이 거리 안에는 칩을 두지 않는다 — 입구가 막혀 시작부터 길이 한 줄로 정해지는 것을 막는다. 넓을수록 칩이 열린 구멍 반대쪽으로 쏠린다.")]
    [Min(0)] public int SpawnClearance = 1;
    [Tooltip("칩 하나당 배치 시도 횟수. 조건에 맞는 자리가 모자랄 때 생성이 끝나도록 보장한다(모자라면 다시 굴린다).")]
    [Min(1)] public int PlaceAttemptsPerTile = 20;
}

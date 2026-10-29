using UnityEngine;

/// <summary>
/// 미로 계획 설정(기획서 11.4) — 봇(MazePlanner)과 맵 생성 검사(MazeRouteProbe)가 같은 담금질·같은 점수를 쓴다.
/// 배치 미리보기 설정에 섞여 있던 것을 뺐다.
/// </summary>
[CreateAssetMenu(menuName = "Def/MazeAssist/Maze Plan Config", fileName = nameof(SO_MazePlanConfig))]
public sealed class SO_MazePlanConfig : SO_Config
{
    [Tooltip("계획할 주사위 총수(놓인 것 포함). 이만큼으로 지을 수 있는 가장 좋은 미로를 찾는다.")]
    [Min(1)] public int TargetDice = 20;
    [Tooltip("판이 바뀔 때마다 돌리는 담금질 걸음 수. 많을수록 좋은 배치를 찾지만 오래 걸린다(워커 스레드에서 돈다).")]
    [Min(0)] public int Iterations = 20000;
    [Tooltip("계획한 칸이 길을 덮는다고 칠 사거리(칸). 색이 정해지지 않은 칸이라 주사위 사거리의 대략값.")]
    [Min(0f)] public float AssumedRange = 2.8f;
    [Tooltip("점수 = 길 길이 + 길 칸마다 사거리 안 주사위 수 × 이 값. 0이면 길 길이만 본다.")]
    [Min(0f)] public float CoverageWeight = 0.3f;
    [Tooltip("보스 구멍의 길을 출현 몫(그 구멍에 들어간 판 주사위 수)에 더해 이만큼 더 쳐 준다. 0이면 출현 몫만 본다.")]
    [Min(0f)] public float BossWeight = 1f;
    [Tooltip("담금질 시작 온도 — 높을수록 처음에 나쁜 배치도 받아들여 넓게 찾는다.")]
    [Min(0.001f)] public float StartTemperature = 3f;
    [Tooltip("담금질 끝 온도.")]
    [Min(0.001f)] public float EndTemperature = 0.05f;
    [Tooltip("맵 생성 검사(MazeRouteProbe)의 담금질 걸음 수 — 판 시작 전에 한 번에 도는 계산이라 Iterations보다 적게 둔다.")]
    [Min(0)] public int ProbeIterations = 3000;
}

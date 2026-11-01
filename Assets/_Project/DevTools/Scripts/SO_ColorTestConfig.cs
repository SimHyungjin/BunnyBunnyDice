using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 색 시험(ColorPowerTest, 테스트 키 T)의 설정. 봇 설정에 섞여 있던 것을 뺐다 — 색 시험은 봇을 쓰지 않고 판 반복·빠른 시뮬 길만 같이 쓴다.
/// </summary>
[CreateAssetMenu(menuName = "Def/DevTools/Color Test Config", fileName = nameof(SO_ColorTestConfig))]
public sealed class SO_ColorTestConfig : SO_Config
{
    [Tooltip("돌릴 색. 맵마다 이 순서로 한 판씩. 검정은 눈 20이면 무작위 색으로 바뀌어 빼 둔다.")]
    public List<DiceColor> Colors = new List<DiceColor>
    {
        DiceColor.Red, DiceColor.Green, DiceColor.Blue,
        DiceColor.Red | DiceColor.Green, DiceColor.Red | DiceColor.Blue, DiceColor.Green | DiceColor.Blue, DiceColors.All,
    };
    [Tooltip("돌릴 맵(시드). 맵마다 모든 색을 돈다.")]
    public List<int> Seeds = new List<int> { 100581265, 56137937, 55854625 };
    [Tooltip("돌릴 사거리(칸). 비우면 색마다 원래 사거리 한 번. 채우면 맵 × 색 × 사거리마다 한 판 — 사거리 1점이 공격력·공격 속도 몇 단계와 맞먹는지 재려고(2026-10-31).")]
    public List<float> Ranges = new List<float>();
    [Tooltip("시험 주사위 수. 0이면 미로 전부. 채우면 미로 모양은 그대로 두고(나머지 칸은 벽으로 막는다) 길을 가장 많이 덮는 칸에만 놓는다 — 미로를 다 채우면 덮는 구간이 겹쳐 사거리 값이 작게 나와 실제 판에 가깝게 재려고(2026-10-31).")]
    [Min(0)] public int MaxDice = 0;
    [Tooltip("켜면 미로를 지은 뒤 몬스터 길 칸만 비우고 나머지 빈 땅을 모두 채운다(MaxDice 무시). 길 옆 칸에만 놓으면 멀리서 셀수록 센 파랑이 불리해 " +
             "색마다 맞는 자리가 다 있을 때를 재려고 — 주사위 15개(좋은 자리가 모자람)와 양 끝으로 읽는다(2026-10-31).")]
    public bool FillOffPath;
    [Tooltip("돌릴 몬스터 특성. 비우면 웨이브 설정 그대로. 채우면 맵 × 색 × 특성마다 한 판 — 보스 사이 웨이브가 모두 그 특성 하나만 달고 나온다(빈 칸 = 특성 없음, 기준선). " +
             "특성마다 막히는 색과 뚫는 색을 재려고(2026-10-31). 보스 웨이브는 웨이브 설정 그대로다.")]
    public List<SO_MonsterTrait> Traits = new List<SO_MonsterTrait>();
    [Tooltip("시험 주사위 면체 — 눈은 상한까지 채운다(d20이면 각성).")]
    [Min(1)] public int Faces = 20;
    [Tooltip("모든 적의 이동 속도 배율 — 디버프가 늘 걸린 셈. 기본 = 둔화 25%(×0.75) × 얼림 0.7초/면역 2초(움직이는 시간 74.1%) ≈ 0.556.")]
    [Range(0.05f, 1f)] public float SpeedScale = 0.556f;
    [Tooltip("이 웨이브에 닿으면 그 판을 끝낸다.")]
    [Min(1)] public int MaxWave = 100;
    [Tooltip("웨이브 처리 비율(깎은 체력 ÷ 들어온 체력)이 이보다 낮아지면 그 판을 끝낸다.")]
    [Range(0f, 1f)] public float StopRatio = 0.1f;
}

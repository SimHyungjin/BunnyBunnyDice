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
    [Tooltip("시험 주사위 면체 — 눈은 상한까지 채운다(d20이면 각성).")]
    [Min(1)] public int Faces = 20;
    [Tooltip("모든 적의 이동 속도 배율 — 디버프가 늘 걸린 셈. 기본 = 둔화 25%(×0.75) × 얼림 0.7초/면역 2초(움직이는 시간 74.1%) ≈ 0.556.")]
    [Range(0.05f, 1f)] public float SpeedScale = 0.556f;
    [Tooltip("이 웨이브에 닿으면 그 판을 끝낸다.")]
    [Min(1)] public int MaxWave = 100;
    [Tooltip("웨이브 처리 비율(깎은 체력 ÷ 들어온 체력)이 이보다 낮아지면 그 판을 끝낸다.")]
    [Range(0f, 1f)] public float StopRatio = 0.1f;
}

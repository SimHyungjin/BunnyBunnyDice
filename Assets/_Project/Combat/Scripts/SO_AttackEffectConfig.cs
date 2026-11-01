using UnityEngine;

/// <summary>
/// 공격선(쏜 쪽에서 맞은 몬스터까지 잠깐 그리는 선) 설정. 주사위·영웅이 같이 쓴다.
/// 크기는 칸 단위(한 칸 = 1), 시간은 초다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Combat/Attack Effect Config", fileName = nameof(SO_AttackEffectConfig))]
public sealed class SO_AttackEffectConfig : SO_Config
{
    [Tooltip("선 머티리얼. 정점 색을 곱하는 반투명 셰이더여야 쏜 쪽 색이 입혀지고 사라질 때 흐려진다.")]
    public Material Material;
    [Tooltip("선이 남아 있다가 사라지는 시간.")]
    [Min(0.01f)] public float Duration = 0.12f;
    public float Width = 0.06f;
    [Tooltip("치명타일 때 굵기 배율.")]
    [Min(1f)] public float CritWidthScale = 2.5f;
    [Tooltip("쏜 쪽 색에 곱하는 밝기. 1을 넘으면 블룸으로 빛난다.")]
    [Min(0f)] public float Intensity = 2f;
    [Header("폭발 고리")]
    [Min(0.01f)] public float BurstDuration = 0.25f;
    public float BurstWidth = 0.08f;
    [Tooltip("처음 고리 크기(반경 대비). 여기서 반경까지 퍼진다.")]
    [Range(0f, 1f)] public float BurstStartScale = 0.3f;
    [Tooltip("고리를 이루는 점 수.")]
    [Min(3)] public int BurstSegments = 48;
    [Tooltip("바닥 위로 띄우는 높이. 고리는 바닥에 눕힌 메시라 바닥과 같은 높이면 깜빡인다.")]
    [Min(0f)] public float BurstLift = 0.03f;

    [Header("오라 (하양 파동)")]
    [Tooltip("파동 주사위가 놓인 동안 사거리에 떠 있는 고리의 투명도. 파동은 공속이 빨라 쏠 때마다 퍼지는 고리가 화면을 덮었다(2026-11-01 사용자) — 늘 떠 있는 연한 고리로 바꿨다.")]
    [Range(0f, 1f)] public float AuraAlpha = 0.1f;
    [Tooltip("파동을 쏠 때 오라가 밝아졌다(투명도 1) 돌아오는 시간 — 공격하는 것이 보이게.")]
    [Min(0.01f)] public float AuraFlashDuration = 0.2f;
    public float AuraWidth = 0.05f;

    [Tooltip("처음 잡아 두는 선 수. 모자라면 더 만든다.")]
    [Min(1)] public int InitialCapacity = 32;
}

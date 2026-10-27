using UnityEngine;

/// <summary>목표물 설정. 몬스터가 닿을 때 깎는 양은 몬스터 종류(SO_MonsterType.GoalDamage)가 정한다.</summary>
[CreateAssetMenu(menuName = "Def/Goal/Goal Config", fileName = nameof(SO_GoalConfig))]
public sealed class SO_GoalConfig : SO_Config
{
    [Tooltip("판 시작 체력. 0이 되면 게임 오버.")]
    [Min(1)] public int MaxHealth = 100;

    [Header("피격 반응 (GoalHitReaction, 표시만)")]
    [Tooltip("몬스터가 닿은 뒤 원래 모습으로 돌아오기까지(초, 실제 시간).")]
    [Min(0f)] public float HitDuration = 0.45f;
    [Tooltip("찌그러지는 세기(세로 비율). 0이면 튀지 않는다.")]
    [Range(0f, 0.9f)] public float HitSquash = 0.35f;
    [Tooltip("잦아드는 동안 출렁이는 횟수.")]
    [Min(0.5f)] public float HitBounces = 1.5f;
    [Tooltip("빛·고리 색(UI 빨강).")]
    public Color HitColor = new Color(1f, 0.32f, 0.42f);
    [Tooltip("닿은 순간의 발광 세기. 1보다 크면 블룸을 받는다.")]
    [Min(0f)] public float HitFlashIntensity = 3f;
    [Tooltip("바닥에 퍼지는 고리 반지름(칸). 0이면 없다.")]
    [Min(0f)] public float HitBurstRadius = 0.9f;
}

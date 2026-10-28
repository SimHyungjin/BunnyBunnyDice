using UnityEngine;

/// <summary>
/// 몬스터 한 종류의 정의(종류마다 에셋 하나). 웨이브 설정(SO_WaveConfig)이 참조하므로 의존성으로 함께 로드된다.
/// 모든 몬스터가 나눠 쓰는 값(반지름 등)은 SO_MonsterConfig에 둔다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Monster/Monster Type", fileName = "SO_MonsterType_")]
public sealed class SO_MonsterType : ScriptableObject
{
    public string DisplayName;
    [Tooltip("발밑이 피벗인 프리팹. 앞(+Z)이 이동 방향을 향하도록 돌린다.")]
    public GameObject Prefab;
    [Tooltip("특성 없는 몹의 공 색. 특성 색도 이 색에 섞는다(SO_MonsterTrait.Tint).")]
    public Color Color = Color.red;

    [Tooltip("초당 칸 수.")]
    [Min(0.1f)] public float MoveSpeed = 1.2f;
    [Tooltip("1웨이브 체력. 웨이브가 오를수록 SO_WaveConfig의 배율(HealthGrowth·HealthCompound)만큼 불어난다.")]
    [Min(1f)] public float Health = 100f;
    [Tooltip("받는 피해에서 빼는 고정값(기획서 9.11 단단함). 0이면 그대로 받는다.")]
    [Min(0f)] public float Armor = 0f;
    [Tooltip("목표물에 닿았을 때 깎는 체력. 일반 1, 보스·현상금 3~5(기획서 3.2).")]
    [Min(0)] public int GoalDamage = 1;
    [Tooltip("잡았을 때 얻는 경험치.")]
    [Min(0)] public int Exp = 1;
}

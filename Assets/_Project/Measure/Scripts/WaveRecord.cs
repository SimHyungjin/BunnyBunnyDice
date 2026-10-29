using System.Collections.Generic;

/// <summary>웨이브 하나의 측정값(기획서 11.2의 4번). WaveLog가 채운다.</summary>
public sealed class WaveRecord
{
    public int Wave;
    /// <summary>시작부터 다 잡을 때까지(게임 시간, 초). 다 잡기 전에 다음 웨이브로 넘어가면 그때까지.</summary>
    public float Seconds;
    /// <summary>다 잡았는지. 건너뛰었거나 진 웨이브는 false.</summary>
    public bool Cleared;
    public int Kills;
    /// <summary>목표물에 닿은 적 수(피해 없는 잭팟 포함).</summary>
    public int Leaked;
    public int GoalDamage;
    /// <summary>웨이브가 끝났을 때의 목표물 체력과 레벨.</summary>
    public int GoalHealth;
    public int Level;
    /// <summary>출처별로 실제로 깎은 체력(DamageSource.Index 순서).</summary>
    public readonly float[] Damage = new float[DamageSource.Count];
    /// <summary>이 웨이브 동안 고른 카드(에셋 이름, 받은 장수가 2 이상이면 "x2"를 붙인다).</summary>
    public readonly List<string> Cards = new List<string>();
    /// <summary>
    /// 나온 몬스터 특성(웨이브 시작 때). 모양마다 특성을 "+"로 잇고 모양끼리는 "|", 보스는 "Boss:"를 붙인다. 특성 없으면 "Normal".
    /// 11웨이브부터 특성이 무작위라, 특성별 정리 시간·피해를 비교하려면 웨이브마다 남겨야 한다(2026-10-28 — 빠름이 보너스처럼 느껴진다는 사용자 체감 확인용).
    /// </summary>
    public string Monsters = string.Empty;
    /// <summary>오버드라이브 상태의 주사위가 낸 피해(DamageBoost 순서, None 칸은 비워 둔다). Damage의 색 몫과 겹친다 — 그중 얼마가 오버드라이브였는지.</summary>
    public readonly float[] BoostDamage = new float[BoostCount];
    /// <summary>영웅이 주사위에 올라타 있던 시간(Seconds와 같은 동안만 잰다).</summary>
    public float RideSeconds;
    /// <summary>영웅 오버드라이브로 깎인 눈 수.</summary>
    public int PipsDrained;
    /// <summary>쓴 스킬 카드 수(모든 대상).</summary>
    public int SkillUses;
    /// <summary>레벨업 후보로 나온 카드 수(CardKind 순서) — 고른 것만으로는 "안 나왔는지 안 골랐는지"가 안 갈려서.</summary>
    public readonly int[] Offers = new int[OfferKinds];
    /// <summary>적 대상 스킬 종류별(EnemySkillStats 순서) 쓴 수·깎은 체력·넘친 피해·처치 수.</summary>
    public readonly int[] EnemySkillUses = new int[EnemySkillStats.Count];
    public readonly float[] EnemySkillDealt = new float[EnemySkillStats.Count];
    public readonly float[] EnemySkillOverkill = new float[EnemySkillStats.Count];
    public readonly int[] EnemySkillKills = new int[EnemySkillStats.Count];
    /// <summary>웨이브가 끝났을 때 판 위 주사위 수·눈 합(색 = DamageSource.Index 순서, 주사위 출처만). 고른 카드 수로 나누면 얹어 섞인 카드가
    /// 섞인 색 몫으로 잡혀 단일형이 약해 보이므로, 판 위 주사위·눈으로 나눠 본다(2026-10-27 사용자 지적).</summary>
    public readonly int[] DiceCount = new int[DiceColorCount];
    public readonly int[] PipSum = new int[DiceColorCount];

    /// <summary>주사위 색 출처 수(DamageSource 0~7번).</summary>
    public const int DiceColorCount = 8;

    public static readonly int BoostCount = System.Enum.GetValues(typeof(DamageBoost)).Length;
    public static readonly int OfferKinds = System.Enum.GetValues(typeof(CardKind)).Length;

    public float TotalDamage
    {
        get
        {
            float total = 0f;
            for (int i = 0; i < Damage.Length; i++) total += Damage[i];
            return total;
        }
    }
}

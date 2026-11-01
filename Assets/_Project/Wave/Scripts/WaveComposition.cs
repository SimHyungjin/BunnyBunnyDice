using System.Collections.Generic;

/// <summary>
/// 웨이브 하나의 몬스터 구성(어떤 특성 조합이 나오는지)을 정하는 규칙. 설정(SO_WaveConfig)과 내보내기(WaveSpawner)에서 떼어 둔다.
/// 목록 안 웨이브는 목록대로, 목록 뒤는 주기(분노 → 재생 → 보호막 → 단단함 → 보스)를 되풀이하고 일정 웨이브부터 무작위 특성을 하나 더한다.
/// 주기가 비어 있으면 무작위 조합이다 — 특성을 하나씩 익힌 뒤에는 조합이 섞여 나와 여러 공격 방식이 필요해지게(기획서 9.1).
/// </summary>
public static class WaveComposition
{
    /// <param name="regular">일반 몬스터 가지마다 특성 목록. 비어 있지 않게 채운다(최소 한 가지).</param>
    /// <param name="boss">보스 특성. 보스가 없으면 비운다.</param>
    /// <returns>보스가 나오는지.</returns>
    public static bool Compose(SO_WaveConfig config, int wave, RandomStream random,
        List<List<SO_MonsterTrait>> regular, List<SO_MonsterTrait> boss)
    {
        regular.Clear();
        boss.Clear();

        if (wave <= config.Waves.Count)
        {
            SO_WaveConfig.WaveEntry entry = config.Waves[wave - 1];
            for (int i = 0; i < entry.Monsters.Count; i++) regular.Add(CopyTraits(entry.Monsters[i].Traits));
            if (regular.Count == 0) regular.Add(new List<SO_MonsterTrait>());
            if (!entry.HasBoss) return false;
            AddNonNull(boss, entry.Boss.Traits);
            return true;
        }

        if (config.CycleTraits.Count > 0) return ComposeCycle(config, wave, random, regular, boss);

        for (int i = 0; i < config.RandomVariants; i++)
        {
            List<SO_MonsterTrait> traits = new List<SO_MonsterTrait>();
            int max = config.RandomTraitMax < config.RandomTraitMin ? config.RandomTraitMin : config.RandomTraitMax;
            AddRandom(traits, config.RandomTraits, random.Range(config.RandomTraitMin, max + 1), random);
            regular.Add(traits);
        }
        if (wave % config.BossInterval != 0) return false;
        AddNonNull(boss, config.BossTraits);
        AddRandom(boss, config.RandomTraits, config.BossRandomTraits, random);
        return true;
    }

    // 보스 웨이브(BossInterval의 배수)의 일반 몬스터는 주기 특성 없이 무작위 몫만 받는다 — 그 웨이브의 주인공은 보스다.
    // 주기 특성 수가 보스 간격과 달라도 차례대로 돈다(보스 웨이브는 차례를 쓰지 않는다).
    private static bool ComposeCycle(SO_WaveConfig config, int wave, RandomStream random,
        List<List<SO_MonsterTrait>> regular, List<SO_MonsterTrait> boss)
    {
        int interval = config.BossInterval;
        bool bossWave = wave % interval == 0;
        SO_MonsterTrait cycle = null;
        if (!bossWave)
        {
            int index = (wave - 1) / interval * (interval - 1) + (wave - 1) % interval;
            cycle = config.CycleTraits[index % config.CycleTraits.Count];
        }
        int extra = wave >= config.CycleExtraFromWave ? config.CycleExtraTraits : 0;
        for (int i = 0; i < config.RandomVariants; i++)
        {
            List<SO_MonsterTrait> traits = new List<SO_MonsterTrait>();
            if (cycle != null) traits.Add(cycle);
            AddRandom(traits, config.RandomTraits, extra, random);
            regular.Add(traits);
        }
        if (!bossWave) return false;
        AddNonNull(boss, config.BossTraits);
        AddRandom(boss, config.RandomTraits, config.BossRandomTraits, random);
        return true;
    }

    private static List<SO_MonsterTrait> CopyTraits(List<SO_MonsterTrait> source)
    {
        List<SO_MonsterTrait> traits = new List<SO_MonsterTrait>();
        AddNonNull(traits, source);
        return traits;
    }

    // 인스펙터에서 빈 칸을 남겨도 무시한다.
    private static void AddNonNull(List<SO_MonsterTrait> target, List<SO_MonsterTrait> source)
    {
        for (int i = 0; i < source.Count; i++)
            if (source[i] != null && !target.Contains(source[i])) target.Add(source[i]);
    }

    // 후보에서 이미 붙은 것을 빼고 count개를 겹치지 않게 고른다. 후보가 모자라면 있는 만큼만.
    private static void AddRandom(List<SO_MonsterTrait> target, List<SO_MonsterTrait> pool, int count, RandomStream random)
    {
        List<SO_MonsterTrait> candidates = new List<SO_MonsterTrait>();
        for (int i = 0; i < pool.Count; i++)
            if (pool[i] != null && !target.Contains(pool[i])) candidates.Add(pool[i]);
        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int pick = random.Next(candidates.Count);
            target.Add(candidates[pick]);
            candidates.RemoveAt(pick);
        }
    }
}

using System.Collections.Generic;

/// <summary>
/// 웨이브 하나의 몬스터 구성(어떤 특성 조합이 나오는지)을 정하는 규칙. 설정(SO_WaveConfig)과 내보내기(WaveSpawner)에서 떼어 둔다.
/// 목록 안 웨이브는 목록대로, 목록 뒤는 주기(분노 → 재생 → 보호막 → 단단함 → 보스)를 되풀이한다.
/// 주기 웨이브는 한 종류만 나온다 — 웨이브 특성 + (일정 웨이브부터) 약하게 받는 보조 특성. 보스 웨이브의 일반 몬스터는
/// 주기 특성을 하나씩 단 몬스터가 무작위로 섞여 나온다(2026-11-02 사용자: 웨이브마다 문제는 하나, 보스 웨이브는 배운 것을 섞어 본다).
/// 주기가 비어 있으면 무작위 조합이다(RandomVariants 가지가 번갈아 나온다).
/// </summary>
public static class WaveComposition
{
    /// <param name="regular">일반 몬스터 가지마다 특성 목록. 비어 있지 않게 채운다(최소 한 가지).</param>
    /// <param name="boss">보스 특성. 보스가 없으면 비운다.</param>
    /// <param name="mixed">참이면 가지를 마리마다 무작위로 고른다(보스 웨이브의 섞어 쏟아지기), 거짓이면 차례로 번갈아.</param>
    /// <returns>보스가 나오는지.</returns>
    public static bool Compose(SO_WaveConfig config, int wave, RandomStream random,
        List<List<TraitSlot>> regular, List<TraitSlot> boss, out bool mixed)
    {
        regular.Clear();
        boss.Clear();
        mixed = false;

        if (wave <= config.Waves.Count)
        {
            SO_WaveConfig.WaveEntry entry = config.Waves[wave - 1];
            for (int i = 0; i < entry.Monsters.Count; i++) regular.Add(ToSlots(entry.Monsters[i].Traits));
            if (regular.Count == 0) regular.Add(new List<TraitSlot>());
            if (!entry.HasBoss) return false;
            AddNonNull(boss, entry.Boss.Traits);
            return true;
        }

        if (config.CycleTraits.Count > 0) return ComposeCycle(config, wave, random, regular, boss, out mixed);

        for (int i = 0; i < config.RandomVariants; i++)
        {
            List<TraitSlot> traits = new List<TraitSlot>();
            int max = config.RandomTraitMax < config.RandomTraitMin ? config.RandomTraitMin : config.RandomTraitMax;
            AddRandom(traits, config.RandomTraits, random.Range(config.RandomTraitMin, max + 1), 1f, random);
            regular.Add(traits);
        }
        if (wave % config.BossInterval != 0) return false;
        AddBossTraits(config, boss, random);
        return true;
    }

    // 주기 특성 수가 보스 간격과 달라도 차례대로 돈다(보스 웨이브는 차례를 쓰지 않는다).
    private static bool ComposeCycle(SO_WaveConfig config, int wave, RandomStream random,
        List<List<TraitSlot>> regular, List<TraitSlot> boss, out bool mixed)
    {
        int interval = config.BossInterval;
        if (wave % interval == 0)
        {
            // 보스 웨이브: 주기 특성 하나짜리 몬스터를 가지마다 하나씩 — 마리마다 무작위로 고른다. 그 웨이브의 주인공은 보스라 보조 특성은 없다.
            mixed = true;
            for (int i = 0; i < config.CycleTraits.Count; i++)
            {
                if (config.CycleTraits[i] == null) continue;
                List<TraitSlot> single = new List<TraitSlot> { new TraitSlot(config.CycleTraits[i]) };
                if (!Contains(regular, config.CycleTraits[i])) regular.Add(single);
            }
            if (regular.Count == 0) regular.Add(new List<TraitSlot>());
            AddBossTraits(config, boss, random);
            return true;
        }

        mixed = false;
        int index = (wave - 1) / interval * (interval - 1) + (wave - 1) % interval;
        SO_MonsterTrait cycle = config.CycleTraits[index % config.CycleTraits.Count];
        List<TraitSlot> traits = new List<TraitSlot>();
        if (cycle != null) traits.Add(new TraitSlot(cycle));
        int extra = wave >= config.CycleExtraFromWave ? config.CycleExtraTraits : 0;
        AddRandom(traits, config.RandomTraits, extra, config.CycleExtraStrength, random);
        regular.Add(traits);
        return false;
    }

    private static bool Contains(List<List<TraitSlot>> variants, SO_MonsterTrait trait)
    {
        for (int i = 0; i < variants.Count; i++)
            if (variants[i].Count == 1 && variants[i][0].Trait == trait) return true;
        return false;
    }

    private static bool Contains(List<TraitSlot> slots, SO_MonsterTrait trait)
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].Trait == trait) return true;
        return false;
    }

    private static List<TraitSlot> ToSlots(List<SO_MonsterTrait> source)
    {
        List<TraitSlot> slots = new List<TraitSlot>();
        AddNonNull(slots, source);
        return slots;
    }

    // 보스 = 늘 붙는 특성(세기 1) + 주기 특성들(BossExtraStrength) + 무작위 몫.
    private static void AddBossTraits(SO_WaveConfig config, List<TraitSlot> boss, RandomStream random)
    {
        AddNonNull(boss, config.BossTraits);
        for (int i = 0; i < config.BossExtraTraits.Count; i++)
        {
            SO_MonsterTrait trait = config.BossExtraTraits[i];
            if (trait != null && !Contains(boss, trait)) boss.Add(new TraitSlot(trait, config.BossExtraStrength));
        }
        AddRandom(boss, config.RandomTraits, config.BossRandomTraits, 1f, random);
    }

    // 인스펙터에서 빈 칸을 남겨도 무시한다.
    private static void AddNonNull(List<TraitSlot> target, List<SO_MonsterTrait> source)
    {
        for (int i = 0; i < source.Count; i++)
            if (source[i] != null && !Contains(target, source[i])) target.Add(new TraitSlot(source[i]));
    }

    // 후보에서 이미 붙은 것을 빼고 count개를 겹치지 않게 고른다. 후보가 모자라면 있는 만큼만.
    private static void AddRandom(List<TraitSlot> target, List<SO_MonsterTrait> pool, int count, float strength, RandomStream random)
    {
        List<SO_MonsterTrait> candidates = new List<SO_MonsterTrait>();
        for (int i = 0; i < pool.Count; i++)
            if (pool[i] != null && !Contains(target, pool[i]) && !candidates.Contains(pool[i])) candidates.Add(pool[i]);
        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int pick = random.Next(candidates.Count);
            target.Add(new TraitSlot(candidates[pick], strength));
            candidates.RemoveAt(pick);
        }
    }
}

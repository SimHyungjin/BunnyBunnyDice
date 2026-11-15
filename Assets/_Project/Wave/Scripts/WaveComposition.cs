using System.Collections.Generic;

/// <summary>
/// 웨이브 하나의 몬스터 구성(어떤 특성 조합이 나오는지)을 정하는 규칙. 설정(SO_WaveConfig)과 내보내기(WaveSpawner)에서 떼어 둔다.
/// 주기(분노 → 재생 → 보호막 → 단단함 → 보스)를 되풀이한다. 주기 웨이브는 한 종류만 나온다 — 웨이브 특성 + (일정 웨이브부터) 약하게 받는 보조 특성.
/// 보스 웨이브의 일반 몬스터는 주기 특성을 하나씩 단 몬스터가 무작위로 섞여 나온다(2026-11-02 사용자: 웨이브마다 문제는 하나, 보스 웨이브는 배운 것을 섞어 본다).
/// 2부의 매 웨이브 보스는 주기 웨이브에 보스만 더한다. 주기가 비어 있으면 특성 없는 몬스터만 나온다(색 시험의 "특성 없음").
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

        bool intervalBoss = wave % config.BossInterval == 0;
        if (intervalBoss && config.CycleTraits.Count > 0)
        {
            // 보스 웨이브: 주기 특성 하나짜리 몬스터를 가지마다 하나씩 — 마리마다 무작위로 고른다. 그 웨이브의 주인공은 보스라 보조 특성은 없다.
            mixed = true;
            for (int i = 0; i < config.CycleTraits.Count; i++)
            {
                if (config.CycleTraits[i] == null) continue;
                if (!Contains(regular, config.CycleTraits[i])) regular.Add(new List<TraitSlot> { new TraitSlot(config.CycleTraits[i]) });
            }
        }
        else if (config.CycleTraits.Count > 0)
        {
            regular.Add(CycleWave(config, wave, random));
        }
        if (regular.Count == 0) regular.Add(new List<TraitSlot>());

        if (!config.HasBoss(wave)) return false;
        AddBossTraits(config, boss);
        return true;
    }

    // 주기 특성 수가 보스 간격과 달라도 차례대로 돈다(보스 간격 웨이브는 차례를 쓰지 않는다).
    private static List<TraitSlot> CycleWave(SO_WaveConfig config, int wave, RandomStream random)
    {
        int interval = config.BossInterval;
        int index = (wave - 1) / interval * (interval - 1) + (wave - 1) % interval;
        SO_MonsterTrait cycle = config.CycleTraits[index % config.CycleTraits.Count];
        List<TraitSlot> traits = new List<TraitSlot>();
        if (cycle != null) traits.Add(new TraitSlot(cycle));
        int extra = wave >= config.CycleExtraFromWave ? config.CycleExtraTraits : 0;
        AddRandom(traits, config.CycleExtraPool, extra, config.CycleExtraStrength, random);
        return traits;
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

    // 보스 = 늘 붙는 특성(세기 1) + 주기 특성들(BossExtraStrength). 인스펙터에서 빈 칸을 남겨도 무시한다.
    private static void AddBossTraits(SO_WaveConfig config, List<TraitSlot> boss)
    {
        for (int i = 0; i < config.BossTraits.Count; i++)
        {
            SO_MonsterTrait trait = config.BossTraits[i];
            if (trait != null && !Contains(boss, trait)) boss.Add(new TraitSlot(trait));
        }
        for (int i = 0; i < config.BossExtraTraits.Count; i++)
        {
            SO_MonsterTrait trait = config.BossExtraTraits[i];
            if (trait != null && !Contains(boss, trait)) boss.Add(new TraitSlot(trait, config.BossExtraStrength));
        }
    }

    // 후보에서 이미 붙은 것을 빼고 count개를 겹치지 않게 고른다. 후보가 모자라면 있는 만큼만.
    private static void AddRandom(List<TraitSlot> target, List<SO_MonsterTrait> pool, int count, float strength, RandomStream random)
    {
        if (count <= 0) return;
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

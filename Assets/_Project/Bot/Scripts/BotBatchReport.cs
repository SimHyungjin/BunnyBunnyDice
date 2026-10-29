using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 봇이 여러 판을 돈 뒤의 요약(기획서 11.4). 판마다 한 줄(깬 웨이브·처음 샌 웨이브·피해 비중·고른 카드 종류)과 맨 아래 평균을 CSV로 남긴다.
/// 판마다의 자세한 기록은 WaveLog CSV가 따로 남으므로, 여기는 판끼리·설정끼리 비교할 숫자만 모은다 — 사람(또는 Claude)이 파일 하나만 읽으면 되게.
/// od_* = 오버드라이브 상태의 주사위가 낸 피해 몫(single·area와 겹친다), ride = 웨이브 시간 중 영웅이 주사위에 올라탄 비율,
/// offer_* = 레벨업 후보로 나온 수(pick_*는 그중 고른 수).
/// 주사위 색마다 <색>_pct(주사위 피해 중 몫), <색>_dice·<색>_pips(웨이브 끝 판 위 주사위 수·눈 합의 웨이브 평균) — 몫을 이것으로 나눠 색 세기를 본다.
/// 적 대상 스킬 종류마다 <이름>_uses(쓴 수), _pct(판 전체 피해 중 몫), _over_pct(낸 피해 중 넘쳐 버려진 몫), _kills(처치 수).
/// </summary>
public sealed class BotBatchReport
{
    private const string Header =
        "run,set,character,stages,seed,maze,wave_cleared,first_leak,minutes,hero_pct,skill_pct,single_pct,area_pct,basic_pct,od_hero_pct,od_card_pct,ride_pct," +
        "pips_drained,skill_uses,pick_dice,pick_skill,pick_chip,pick_hero,offer_dice,offer_skill,offer_chip,offer_hero";

    // 피해 출처 번호(DamageSource.Index = 주사위 색 비트): 단일 = 빨강·초록·파랑, 범위 = 섞인 색·하양, 기본 = 검정.
    private static readonly int[] SingleSources = { (int)DiceColor.Red, (int)DiceColor.Green, (int)DiceColor.Blue };

    private sealed class Run
    {
        public int Seed;
        public string Set;
        public string Character;
        public string Stages;
        public string Maze;
        public int WaveCleared;
        public int FirstLeak;
        public float Minutes;
        public float Hero, Skill, Single, Area, Basic;
        public float OverdriveHero, OverdriveCard, Ride;
        public int PipsDrained, SkillUses;
        public int PickDice, PickSkill, PickChip, PickHero;
        public readonly int[] Offers = new int[WaveRecord.OfferKinds];
        public readonly int[] EnemySkillUses = new int[EnemySkillStats.Count];
        public readonly float[] EnemySkillDealt = new float[EnemySkillStats.Count];
        public readonly float[] EnemySkillOverkill = new float[EnemySkillStats.Count];
        public readonly int[] EnemySkillKills = new int[EnemySkillStats.Count];
        public readonly float[] ColorShare = new float[WaveRecord.DiceColorCount];
        public readonly float[] ColorDice = new float[WaveRecord.DiceColorCount];
        public readonly float[] ColorPips = new float[WaveRecord.DiceColorCount];
    }

    // 머리글 뒤에 적 대상 스킬 칸을 붙인다(종류 수가 바뀌어도 맞게).
    private static string BuildHeader()
    {
        StringBuilder builder = new StringBuilder(Header);
        for (int i = 0; i < EnemySkillStats.Count; i++)
        {
            string name = EnemySkillStats.Names[i];
            builder.Append(',').Append(name).Append("_uses,").Append(name).Append("_pct,").Append(name).Append("_over_pct,").Append(name).Append("_kills");
        }
        for (int i = 0; i < WaveRecord.DiceColorCount; i++)
        {
            string name = DamageSource.NameOf(i).ToLowerInvariant();
            builder.Append(',').Append(name).Append("_pct,").Append(name).Append("_dice,").Append(name).Append("_pips");
        }
        return builder.ToString();
    }

    private readonly List<Run> _runs = new List<Run>();

    public int Count => _runs.Count;

    public void Clear() => _runs.Clear();

    /// <summary>한 판을 더한다. records는 그 판의 웨이브 기록(WaveLog.Records), stages는 능력치 단계("5-6-3-5-1-2").</summary>
    public void Add(IReadOnlyList<WaveRecord> records, string character, string set, string stages, int seed, int waveCleared, string maze)
    {
        Run run = new Run { Set = set, Character = character, Stages = stages, Seed = seed, Maze = maze, WaveCleared = waveCleared, FirstLeak = -1 };
        float total = 0f;
        float seconds = 0f;
        for (int r = 0; r < records.Count; r++)
        {
            WaveRecord record = records[r];
            if (run.FirstLeak < 0 && record.Leaked > 0) run.FirstLeak = record.Wave;
            run.Minutes += record.Seconds / 60f;
            seconds += record.Seconds;
            run.Ride += record.RideSeconds;
            run.OverdriveHero += record.BoostDamage[(int)DamageBoost.Hero];
            run.OverdriveCard += record.BoostDamage[(int)DamageBoost.Card];
            run.PipsDrained += record.PipsDrained;
            run.SkillUses += record.SkillUses;
            for (int k = 0; k < run.Offers.Length; k++) run.Offers[k] += record.Offers[k];
            for (int k = 0; k < WaveRecord.DiceColorCount; k++)
            {
                run.ColorShare[k] += record.Damage[k];
                run.ColorDice[k] += record.DiceCount[k];
                run.ColorPips[k] += record.PipSum[k];
            }
            for (int k = 0; k < EnemySkillStats.Count; k++)
            {
                run.EnemySkillUses[k] += record.EnemySkillUses[k];
                run.EnemySkillDealt[k] += record.EnemySkillDealt[k];
                run.EnemySkillOverkill[k] += record.EnemySkillOverkill[k];
                run.EnemySkillKills[k] += record.EnemySkillKills[k];
            }
            for (int i = 0; i < DamageSource.Count; i++)
            {
                float amount = record.Damage[i];
                total += amount;
                if (i == DamageSource.Hero.Index) run.Hero += amount;
                else if (i == DamageSource.Skill.Index) run.Skill += amount;
                else if (i == 0) run.Basic += amount;
                else if (IsSingle(i)) run.Single += amount;
                else if (i < DiceSourceCount) run.Area += amount;
            }
            for (int c = 0; c < record.Cards.Count; c++) CountPick(run, record.Cards[c]);
        }
        if (total > 0f)
        {
            run.Hero /= total;
            run.Skill /= total;
            run.Single /= total;
            run.Area /= total;
            run.Basic /= total;
            run.OverdriveHero /= total;
            run.OverdriveCard /= total;
            // 넘친 몫은 낸 피해(깎은 + 넘친) 대비로 바꾸기 전에 깎은 체력으로 계산한다.
            for (int k = 0; k < EnemySkillStats.Count; k++)
            {
                float spent = run.EnemySkillDealt[k] + run.EnemySkillOverkill[k];
                run.EnemySkillOverkill[k] = spent > 0f ? run.EnemySkillOverkill[k] / spent : 0f;
                run.EnemySkillDealt[k] /= total;
            }
        }
        if (seconds > 0f) run.Ride /= seconds;
        float diceTotal = 0f;
        for (int k = 0; k < WaveRecord.DiceColorCount; k++) diceTotal += run.ColorShare[k];
        for (int k = 0; k < WaveRecord.DiceColorCount; k++)
        {
            if (diceTotal > 0f) run.ColorShare[k] /= diceTotal;
            if (records.Count == 0) continue;
            run.ColorDice[k] /= records.Count;
            run.ColorPips[k] /= records.Count;
        }
        _runs.Add(run);
    }

    /// <summary>요약 CSV를 쓰고 경로를 돌려준다. 판이 없으면 null.</summary>
    public string Write()
    {
        if (_runs.Count == 0) return null;
        CultureInfo invariant = CultureInfo.InvariantCulture;
        StringBuilder builder = new StringBuilder();
        builder.Append(BuildHeader()).Append('\n');
        Run sum = new Run();
        float leakSum = 0f;
        int leakRuns = 0;
        for (int i = 0; i < _runs.Count; i++)
        {
            Run run = _runs[i];
            AppendRow(builder, (i + 1).ToString(invariant), run, invariant);
            sum.WaveCleared += run.WaveCleared;
            sum.Minutes += run.Minutes;
            sum.Hero += run.Hero;
            sum.Skill += run.Skill;
            sum.Single += run.Single;
            sum.Area += run.Area;
            sum.Basic += run.Basic;
            sum.OverdriveHero += run.OverdriveHero;
            sum.OverdriveCard += run.OverdriveCard;
            sum.Ride += run.Ride;
            sum.PipsDrained += run.PipsDrained;
            sum.SkillUses += run.SkillUses;
            for (int k = 0; k < sum.Offers.Length; k++) sum.Offers[k] += run.Offers[k];
            for (int k = 0; k < WaveRecord.DiceColorCount; k++)
            {
                sum.ColorShare[k] += run.ColorShare[k];
                sum.ColorDice[k] += run.ColorDice[k];
                sum.ColorPips[k] += run.ColorPips[k];
            }
            for (int k = 0; k < EnemySkillStats.Count; k++)
            {
                sum.EnemySkillUses[k] += run.EnemySkillUses[k];
                sum.EnemySkillDealt[k] += run.EnemySkillDealt[k];
                sum.EnemySkillOverkill[k] += run.EnemySkillOverkill[k];
                sum.EnemySkillKills[k] += run.EnemySkillKills[k];
            }
            sum.PickDice += run.PickDice;
            sum.PickSkill += run.PickSkill;
            sum.PickChip += run.PickChip;
            sum.PickHero += run.PickHero;
            if (run.FirstLeak < 0) continue;
            leakSum += run.FirstLeak;
            leakRuns++;
        }
        float n = _runs.Count;
        builder.Append("avg,,,,,,")
            .Append((sum.WaveCleared / n).ToString("0.0", invariant)).Append(',')
            .Append(leakRuns > 0 ? (leakSum / leakRuns).ToString("0.0", invariant) : "").Append(',')
            .Append((sum.Minutes / n).ToString("0.0", invariant)).Append(',')
            .Append(Percent(sum.Hero / n)).Append(',')
            .Append(Percent(sum.Skill / n)).Append(',')
            .Append(Percent(sum.Single / n)).Append(',')
            .Append(Percent(sum.Area / n)).Append(',')
            .Append(Percent(sum.Basic / n)).Append(',')
            .Append(Percent(sum.OverdriveHero / n)).Append(',')
            .Append(Percent(sum.OverdriveCard / n)).Append(',')
            .Append(Percent(sum.Ride / n)).Append(',')
            .Append((sum.PipsDrained / n).ToString("0.0", invariant)).Append(',')
            .Append((sum.SkillUses / n).ToString("0.0", invariant)).Append(',')
            .Append((sum.PickDice / n).ToString("0.0", invariant)).Append(',')
            .Append((sum.PickSkill / n).ToString("0.0", invariant)).Append(',')
            .Append((sum.PickChip / n).ToString("0.0", invariant)).Append(',')
            .Append((sum.PickHero / n).ToString("0.0", invariant));
        AppendOffers(builder, sum, n, invariant);
        AppendEnemySkills(builder, sum, n, invariant);
        AppendColors(builder, sum, n, invariant);
        builder.Append('\n');

        string path = Path.Combine(Application.persistentDataPath, "RunLogs", $"bot_summary_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BotBatchReport] 요약을 쓰지 못했다: {path}\n{e.Message}");
            return null;
        }
        return path;
    }

    private static void AppendRow(StringBuilder builder, string label, Run run, CultureInfo invariant)
    {
        builder.Append(label).Append(',')
            .Append(run.Set).Append(',')
            .Append(run.Character).Append(',')
            .Append(run.Stages).Append(',')
            .Append(run.Seed).Append(',')
            .Append(run.Maze).Append(',')
            .Append(run.WaveCleared).Append(',')
            .Append(run.FirstLeak >= 0 ? run.FirstLeak.ToString(invariant) : "").Append(',')
            .Append(run.Minutes.ToString("0.0", invariant)).Append(',')
            .Append(Percent(run.Hero)).Append(',')
            .Append(Percent(run.Skill)).Append(',')
            .Append(Percent(run.Single)).Append(',')
            .Append(Percent(run.Area)).Append(',')
            .Append(Percent(run.Basic)).Append(',')
            .Append(Percent(run.OverdriveHero)).Append(',')
            .Append(Percent(run.OverdriveCard)).Append(',')
            .Append(Percent(run.Ride)).Append(',')
            .Append(run.PipsDrained).Append(',')
            .Append(run.SkillUses).Append(',')
            .Append(run.PickDice).Append(',')
            .Append(run.PickSkill).Append(',')
            .Append(run.PickChip).Append(',')
            .Append(run.PickHero);
        AppendOffers(builder, run, 1f, invariant);
        AppendEnemySkills(builder, run, 1f, invariant);
        AppendColors(builder, run, 1f, invariant);
        builder.Append('\n');
    }

    // 넘친 몫의 평균은 판마다의 비율을 그냥 평균한다(쓰지 않은 판의 0도 들어간다 — 쓴 수를 같이 본다).
    private static void AppendEnemySkills(StringBuilder builder, Run run, float divide, CultureInfo invariant)
    {
        string countFormat = divide > 1f ? "0.0" : "0";
        for (int k = 0; k < EnemySkillStats.Count; k++)
        {
            builder.Append(',').Append((run.EnemySkillUses[k] / divide).ToString(countFormat, invariant))
                .Append(',').Append(Percent(run.EnemySkillDealt[k] / divide))
                .Append(',').Append(Percent(run.EnemySkillOverkill[k] / divide))
                .Append(',').Append((run.EnemySkillKills[k] / divide).ToString(countFormat, invariant));
        }
    }

    // 머리글 순서(dice·skill·chip·hero)로 쓴다 — CardKind 순서(Dice·Skill·Chip·Hero)와 같다.
    private static void AppendOffers(StringBuilder builder, Run run, float divide, CultureInfo invariant)
    {
        for (int k = 0; k < run.Offers.Length; k++)
            builder.Append(',').Append((run.Offers[k] / divide).ToString(divide > 1f ? "0.0" : "0", invariant));
    }

    private static void AppendColors(StringBuilder builder, Run run, float divide, CultureInfo invariant)
    {
        for (int k = 0; k < WaveRecord.DiceColorCount; k++)
        {
            builder.Append(',').Append(Percent(run.ColorShare[k] / divide))
                .Append(',').Append((run.ColorDice[k] / divide).ToString("0.0", invariant))
                .Append(',').Append((run.ColorPips[k] / divide).ToString("0.0", invariant));
        }
    }

    private static string Percent(float ratio) => (ratio * 100f).ToString("0.0", CultureInfo.InvariantCulture);

    // 주사위 출처는 색 비트 0~7번이다(DamageSource.Dice).
    private const int DiceSourceCount = 8;

    private static bool IsSingle(int index)
    {
        for (int i = 0; i < SingleSources.Length; i++)
            if (SingleSources[i] == index) return true;
        return false;
    }

    // 카드 이름(WaveLog.CountCard): 주사위는 "d12_Red", 나머지는 에셋 이름(SO_SkillCard_… 등), 장수가 2 이상이면 " x2".
    private static void CountPick(Run run, string name)
    {
        int count = 1;
        int x = name.LastIndexOf(" x", StringComparison.Ordinal);
        if (x >= 0 && int.TryParse(name.Substring(x + 2), out int parsed)) count = parsed;
        if (name.StartsWith("SO_SkillCard", StringComparison.Ordinal)) run.PickSkill += count;
        else if (name.StartsWith("SO_ChipCard", StringComparison.Ordinal)) run.PickChip += count;
        else if (name.StartsWith("SO_HeroCard", StringComparison.Ordinal)) run.PickHero += count;
        else run.PickDice += count;
    }
}

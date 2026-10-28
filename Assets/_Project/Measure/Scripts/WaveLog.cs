using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 판 동안 웨이브마다 측정값을 모으고 CSV로 남긴다(기획서 11.2의 4번). 판 시스템의 이벤트를 GameScene이 이어 주고,
/// 여기서는 받은 값만 센다 — 나중에 시뮬레이션이 같은 기록기를 그대로 붙여 쓰게.
/// 앞 웨이브의 남은 적이 다음 웨이브 동안 죽거나 새면 다음 웨이브 몫으로 센다(웨이브가 겹치는 일은 건너뛰기 말고는 드물다).
/// CSV는 웨이브가 끝날 때마다 통째로 다시 쓴다 — 판 도중 로비로 나가거나 에디터를 멈춰도 거기까지 남게(한 판이 수백 줄을 넘지 않는다).
/// </summary>
public sealed class WaveLog : IDisposable
{
    private readonly List<WaveRecord> _records = new List<WaveRecord>();
    private readonly Func<int> _getGoalHealth;
    private readonly Func<int> _getLevel;
    private readonly Func<bool> _isRiding;
    private readonly Func<IReadOnlyList<PlacedDice>> _getDice;
    private readonly StringBuilder _builder = new StringBuilder();
    private WaveRecord _current;
    private string _path;

    /// <summary>지난 웨이브들(지금 웨이브 제외).</summary>
    public IReadOnlyList<WaveRecord> Records => _records;

    /// <summary>진행 중인 웨이브. 첫 웨이브 전·판이 끝난 뒤에는 null.</summary>
    public WaveRecord Current => _current;

    /// <summary>이번 판의 CSV 경로. 아직 쓰지 않았으면 null.</summary>
    public string FilePath => _path;

    /// <param name="isRiding">영웅이 주사위에 올라타 있는지(HeroOverdrive.Active).</param>
    /// <param name="getDice">판 위 주사위(DiceField.Placed) — 웨이브가 끝날 때 색별 수·눈을 센다.</param>
    public WaveLog(Func<int> getGoalHealth, Func<int> getLevel, Func<bool> isRiding, Func<IReadOnlyList<PlacedDice>> getDice)
    {
        _getGoalHealth = getGoalHealth;
        _getLevel = getLevel;
        _isRiding = isRiding;
        _getDice = getDice;
        Core.Loop.OnGameUpdate += Tick;
    }

    public void Dispose() => Core.Loop.OnGameUpdate -= Tick;

    /// <summary>새 판. 파일 이름에 캐릭터와 맵 시드를 넣는다 — 같은 맵으로 수치만 바꿔 비교할 때 찾기 쉽게.</summary>
    public void Begin(string character, int seed)
    {
        _records.Clear();
        _current = null;
        string name = $"run_{DateTime.Now:yyyyMMdd_HHmmss}_{character}_seed{seed}.csv";
        _path = Path.Combine(Application.persistentDataPath, "RunLogs", name);
    }

    /// <summary>판이 끝났다(게임 오버·로비로). 진행 중인 웨이브를 닫고 쓴다.</summary>
    public void End()
    {
        if (_current == null) return;
        Close();
        Write();
    }

    /// <param name="kinds">이번 웨이브 일반 몬스터 모양(WaveSpawner.WaveKinds).</param>
    /// <param name="boss">이번 웨이브 보스 모양. 없으면 null.</param>
    public void OnWaveStarted(int wave, IReadOnlyList<MonsterKind> kinds, MonsterKind boss)
    {
        if (_current != null) Close();
        _current = new WaveRecord { Wave = wave, Monsters = DescribeMonsters(kinds, boss) };
        Write();
    }

    private string DescribeMonsters(IReadOnlyList<MonsterKind> kinds, MonsterKind boss)
    {
        _builder.Clear();
        for (int i = 0; i < kinds.Count; i++)
        {
            if (i > 0) _builder.Append('|');
            AppendTraits(kinds[i]);
        }
        if (boss != null)
        {
            if (_builder.Length > 0) _builder.Append('|');
            _builder.Append("Boss:");
            AppendTraits(boss);
        }
        return _builder.ToString();
    }

    // 에셋 이름에서 공통 앞머리를 떼어 짧게 쓴다(SO_MonsterTrait_Fast → Fast).
    private void AppendTraits(MonsterKind kind)
    {
        const string prefix = "SO_MonsterTrait_";
        IReadOnlyList<SO_MonsterTrait> traits = kind.Traits;
        if (traits.Count == 0)
        {
            _builder.Append("Normal");
            return;
        }
        for (int i = 0; i < traits.Count; i++)
        {
            if (i > 0) _builder.Append('+');
            string name = traits[i].name;
            _builder.Append(name.StartsWith(prefix, StringComparison.Ordinal) ? name.Substring(prefix.Length) : name);
        }
    }

    public void OnWaveCleared(int wave)
    {
        if (_current != null && _current.Wave == wave) _current.Cleared = true;
    }

    public void CountDamage(DamageSource source, float amount)
    {
        if (_current == null) return;
        _current.Damage[source.Index] += amount;
        if (source.Boost != DamageBoost.None) _current.BoostDamage[(int)source.Boost] += amount;
    }

    public void CountPipDrained(Vector2Int _)
    {
        if (_current != null) _current.PipsDrained++;
    }

    public void CountSkillUse(SO_SkillCard _)
    {
        if (_current != null) _current.SkillUses++;
    }

    public void CountAreaCast(SO_AreaSkillCard card, AreaCastResult result)
    {
        int index = AreaSkillStats.IndexOf(card);
        if (_current == null || index < 0) return;
        _current.AreaUses[index]++;
        _current.AreaDealt[index] += result.Dealt;
        _current.AreaOverkill[index] += result.Overkill;
        _current.AreaKills[index] += result.Kills;
    }

    /// <summary>레벨업 후보가 나왔다(고르기 전, 한 번 뜰 때마다).</summary>
    public void CountOffers(IReadOnlyList<HandCard> candidates)
    {
        if (_current == null) return;
        for (int i = 0; i < candidates.Count; i++) _current.Offers[(int)candidates[i].Kind]++;
    }

    public void CountKill()
    {
        if (_current != null) _current.Kills++;
    }

    public void CountLeak(int goalDamage)
    {
        if (_current == null) return;
        _current.Leaked++;
        _current.GoalDamage += goalDamage;
    }

    public void CountCard(HandCard card, int count)
    {
        if (_current == null) return;
        string name = card.Asset != null ? card.Asset.name : $"d{card.Dice.Faces}_{DamageSource.NameOf((int)DiceColors.Normalize(card.Dice.Colors))}";
        _current.Cards.Add(count > 1 ? $"{name} x{count}" : name);
    }

    // 게임 시간을 따른다 — 판 중 설정(멈춤)·카드 고르기(감속) 동안 정리 시간이 실제보다 길어지지 않게.
    private void Tick(float deltaTime)
    {
        if (_current == null || _current.Cleared) return;
        _current.Seconds += deltaTime;
        if (_isRiding()) _current.RideSeconds += deltaTime;
    }

    private void Close()
    {
        _current.GoalHealth = _getGoalHealth();
        _current.Level = _getLevel();
        IReadOnlyList<PlacedDice> dice = _getDice();
        for (int i = 0; i < dice.Count; i++)
        {
            int color = DamageSource.Dice(dice[i].Dice.Colors).Index;
            if (color < 0 || color >= WaveRecord.DiceColorCount) continue;
            _current.DiceCount[color]++;
            _current.PipSum[color] += dice[i].Dice.Pips;
        }
        _records.Add(_current);
        _current = null;
    }

    // 테스트·분석용이라 출시 빌드에서는 쓰지 않는다(기록만 모은다).
    private void Write()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_path == null) return;
        _builder.Clear();
        _builder.Append("wave,seconds,cleared,kills,leaked,goal_damage,goal_health,level,total_damage");
        for (int i = 0; i < DamageSource.Count; i++) _builder.Append(',').Append(DamageSource.NameOf(i));
        _builder.Append(",od_hero,od_card,ride_seconds,pips_drained,skill_uses");
        for (int i = 0; i < WaveRecord.OfferKinds; i++) _builder.Append(",offer_").Append(((CardKind)i).ToString().ToLowerInvariant());
        for (int i = 0; i < AreaSkillStats.Count; i++)
        {
            string name = AreaSkillStats.Names[i];
            _builder.Append(',').Append(name).Append("_uses,").Append(name).Append("_dealt,").Append(name).Append("_over,").Append(name).Append("_kills");
        }
        for (int i = 0; i < WaveRecord.DiceColorCount; i++) _builder.Append(",dice_").Append(DamageSource.NameOf(i));
        for (int i = 0; i < WaveRecord.DiceColorCount; i++) _builder.Append(",pips_").Append(DamageSource.NameOf(i));
        _builder.Append(",monsters,cards\n");
        for (int r = 0; r < _records.Count; r++) AppendRow(_records[r]);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, _builder.ToString(), new UTF8Encoding(true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WaveLog] 기록을 쓰지 못했다: {_path}\n{e.Message}");
        }
#endif
    }

    private void AppendRow(WaveRecord record)
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;
        _builder.Append(record.Wave).Append(',')
            .Append(record.Seconds.ToString("0.0", invariant)).Append(',')
            .Append(record.Cleared ? 1 : 0).Append(',')
            .Append(record.Kills).Append(',')
            .Append(record.Leaked).Append(',')
            .Append(record.GoalDamage).Append(',')
            .Append(record.GoalHealth).Append(',')
            .Append(record.Level).Append(',')
            .Append(record.TotalDamage.ToString("0", invariant));
        for (int i = 0; i < DamageSource.Count; i++) _builder.Append(',').Append(record.Damage[i].ToString("0", invariant));
        _builder.Append(',').Append(record.BoostDamage[(int)DamageBoost.Hero].ToString("0", invariant))
            .Append(',').Append(record.BoostDamage[(int)DamageBoost.Card].ToString("0", invariant))
            .Append(',').Append(record.RideSeconds.ToString("0.0", invariant))
            .Append(',').Append(record.PipsDrained)
            .Append(',').Append(record.SkillUses);
        for (int i = 0; i < WaveRecord.OfferKinds; i++) _builder.Append(',').Append(record.Offers[i]);
        for (int i = 0; i < AreaSkillStats.Count; i++)
        {
            _builder.Append(',').Append(record.AreaUses[i])
                .Append(',').Append(record.AreaDealt[i].ToString("0", invariant))
                .Append(',').Append(record.AreaOverkill[i].ToString("0", invariant))
                .Append(',').Append(record.AreaKills[i]);
        }
        for (int i = 0; i < WaveRecord.DiceColorCount; i++) _builder.Append(',').Append(record.DiceCount[i]);
        for (int i = 0; i < WaveRecord.DiceColorCount; i++) _builder.Append(',').Append(record.PipSum[i]);
        // 이름에 쉼표가 들어가도 칸이 밀리지 않게 따옴표로 감싼다.
        _builder.Append(",\"").Append(record.Monsters.Replace("\"", "\"\"")).Append('"');
        _builder.Append(",\"").Append(string.Join("|", record.Cards).Replace("\"", "\"\"")).Append("\"\n");
    }
}

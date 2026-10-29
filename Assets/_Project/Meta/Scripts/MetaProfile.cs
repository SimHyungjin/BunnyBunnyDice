using System;
using UnityEngine;

/// <summary>
/// 판 밖 진행(기획서 12.4)의 규칙. 세이브(MetaSave)를 읽고 고치며, 영웅 능력치(HeroStats)의 캐릭터·단계를 세이브와 잇는다.
/// 판 결과는 측정 요약(RunSummary)으로만 받는다 — 판 시스템을 모르게(12.4).
///
/// 캐릭터를 처음 열 때 능력치마다 6면 주사위로 단계를 굴린다(10.3). 해금 조건(SO_MetaConfig)이 없는 캐릭터는 처음부터 열려 있고,
/// 조건이 있는 캐릭터는 조건을 채워도 저절로 열리지 않는다 — 선택 창에서 눌러 연다(CanUnlock → Unlock). 여는 순간과 첫 능력치 굴림에
/// 나중에 연출을 붙이려고, 판 결과 화면이 아니라 플레이어가 고르는 자리로 뺐다.
/// 판 보상 = 클리어한 웨이브(RunSummary.Wave) × 주사위 눈(12.4). 리롤은 능력치 하나를 다시 굴리고 낮거나 같으면 그대로 둔다(10.3).
/// 잠긴 캐릭터도 테스트 창(F1)으로 고를 수 있다 — 그때 단계는 세이브에 있으면 그것, 없으면 모두 6단계이고, 고치면 세이브에 남는다
/// (나중에 열면 단계를 새로 굴린다).
/// </summary>
public sealed class MetaProfile : IDisposable
{
    private readonly SO_HeroConfig _config;
    private readonly SO_MetaConfig _metaConfig;
    private readonly MetaSave _save;
    private HeroStats _stats;
    // 세이브 값을 능력치에 넣는 동안 StagesChanged가 되돌아와 다시 쓰지 않게.
    private bool _applying;

    /// <summary>보이는 값(재화·기록·열림)이 바뀌었다 — 로비가 다시 그린다.</summary>
    public event Action Changed;

    /// <summary>캐릭터가 열렸다(능력치 단계를 굴려 넣은 뒤). 처음부터 열린 캐릭터는 프로필을 만들 때 열리므로 들을 쪽이 없다.</summary>
    public event Action<SO_HeroCharacter> Unlocked;

    /// <summary>마지막 판의 보상(Record에서 정해진다). 결과 화면이 보인다.</summary>
    public RunReward LastReward { get; private set; }

    public MetaProfile(SO_HeroConfig config, SO_MetaConfig metaConfig)
    {
        _config = config;
        _metaConfig = metaConfig;
        _save = Core.Data.Get<MetaSave>();
        UnlockFreeCharacters();
        Core.Data.OnDataReplaced += OnDataReplaced;
        // 실행 중에 목록에서 조건을 빼면(GameTuning) 바로 열린다. 조건 숫자를 낮추면 선택 창에서 열 수 있게 된다.
        _metaConfig.Changed += UnlockFreeCharacters;
    }

    public void Dispose()
    {
        Core.Data.OnDataReplaced -= OnDataReplaced;
        _metaConfig.Changed -= UnlockFreeCharacters;
        if (_stats == null) return;
        _stats.CharacterChanged -= OnCharacterChanged;
        _stats.StagesChanged -= OnStagesChanged;
        _stats = null;
    }

    /// <summary>세이브 키(SO_HeroCharacter.Id — 비면 에셋 이름). 에셋 이름을 바꿔도 기록이 끊기지 않게 따로 둔다. 실행 중 복사본(ConfigTuning)도 값을 그대로 가져 같은 키다.</summary>
    public static string IdOf(SO_HeroCharacter character) => string.IsNullOrEmpty(character.Id) ? character.name : character.Id;

    public int Currency => _save.Currency;

    /// <summary>모든 캐릭터를 통틀어 최고 클리어 웨이브.</summary>
    public int BestWave => _save.BestWave;

    public int GetBestWave(SO_HeroCharacter character) =>
        character != null && _save.Characters.TryGetValue(IdOf(character), out CharacterSave save) ? save.BestWave : 0;

    public bool IsUnlocked(SO_HeroCharacter character) =>
        character != null && _save.Characters.TryGetValue(IdOf(character), out CharacterSave save) && save.Unlocked;

    /// <summary>해금 조건. 없으면(처음부터 열린 캐릭터) false.</summary>
    public bool TryGetRule(SO_HeroCharacter character, out UnlockRule rule) => _metaConfig.TryGetRule(character, out rule);

    /// <summary>조건을 채웠지만 아직 열지 않은 캐릭터(선택 창에서 눌러 연다).</summary>
    public bool CanUnlock(SO_HeroCharacter character) =>
        character != null && !IsUnlocked(character) && _metaConfig.TryGetRule(character, out UnlockRule rule) &&
        GetProgress(rule.Kind) >= rule.Target;

    public int RerollCost => _metaConfig.RerollCost;

    /// <summary>지금 캐릭터의 이 능력치를 다시 굴릴 수 있는가: 열린 캐릭터 · 최대 단계가 아님 · 재화가 충분함.</summary>
    public bool CanReroll(HeroStat stat) =>
        _stats != null && IsUnlocked(_stats.Character) && _stats.GetStage(stat) < HeroStats.MaxStage &&
        _save.Currency >= _metaConfig.RerollCost;

    /// <summary>
    /// 지금 캐릭터의 능력치 하나를 재화를 내고 다시 굴린다. 지금보다 높을 때만 바꾼다(단계는 내려가지 않는다, 10.3).
    /// 굴린 눈을 돌려준다(못 굴리면 0) — 나중에 굴리는 연출이 쓴다.
    /// </summary>
    public int Reroll(HeroStat stat)
    {
        if (!CanReroll(stat)) return 0;
        _save.Currency -= _metaConfig.RerollCost;
        _save.MarkDirty();
        int rolled = RollStage();
        // 단계가 바뀌면 OnStagesChanged가 세이브에 남긴다.
        if (rolled > _stats.GetStage(stat)) _stats.SetStage(stat, rolled);
        Changed?.Invoke();
        return rolled;
    }

    /// <summary>조건 종류의 지금 진행도.</summary>
    public int GetProgress(UnlockKind kind)
    {
        switch (kind)
        {
            case UnlockKind.BestWave: return _save.BestWave;
            case UnlockKind.Awakenings: return _save.Awakenings;
            case UnlockKind.JackpotKills: return _save.JackpotKills;
            case UnlockKind.RunCount: return _save.RunCount;
            case UnlockKind.DicePlaced: return _save.DicePlaced;
            default: return 0;
        }
    }

    /// <summary>캐릭터를 연다. 능력치 단계를 새로 굴린다(이미 열려 있으면 아무 일도 없다). 조건은 보지 않는다 — 부르는 쪽이 CanUnlock으로 확인한다.</summary>
    public void Unlock(SO_HeroCharacter character)
    {
        if (character == null) return;
        CharacterSave save = GetOrCreate(character);
        if (save.Unlocked) return;
        save.Unlocked = true;
        save.Stages = new int[HeroStats.StatCount];
        for (int i = 0; i < save.Stages.Length; i++) save.Stages[i] = RollStage();
        _save.MarkDirty();
        if (_stats != null && _stats.Character == character) ApplyStages();
        Changed?.Invoke();
        Unlocked?.Invoke(character);
    }

    /// <summary>재화를 더한다(음수면 뺀다, 0 밑으로는 내려가지 않는다).</summary>
    public void AddCurrency(int amount)
    {
        _save.Currency = Mathf.Max(_save.Currency + amount, 0);
        _save.MarkDirty();
        Changed?.Invoke();
    }

    /// <summary>판 하나가 끝났다. 기록·해금 진행도를 더하고 보상(클리어한 웨이브 × 보상 주사위 눈의 합)을 준다.</summary>
    public void Record(SO_HeroCharacter character, RunSummary run)
    {
        int wave = run.Wave;
        int[] rolls = new int[wave / Mathf.Max(_metaConfig.RewardWavesPerDie, 1)];
        int sum = 0;
        for (int i = 0; i < rolls.Length; i++)
        {
            rolls[i] = UnityEngine.Random.Range(1, _metaConfig.RewardDieFaces + 1);
            sum += rolls[i];
        }
        LastReward = new RunReward { Wave = wave, Rolls = rolls, Sum = sum, Amount = wave * sum };
        _save.Currency += LastReward.Amount;
        _save.RunCount++;
        _save.BestWave = Mathf.Max(_save.BestWave, run.Wave);
        _save.JackpotKills += run.JackpotKills;
        _save.DicePlaced += run.DicePlaced;
        _save.Awakenings += run.Awakenings;
        if (character != null)
        {
            CharacterSave save = GetOrCreate(character);
            save.BestWave = Mathf.Max(save.BestWave, run.Wave);
        }
        _save.MarkDirty();
        Changed?.Invoke();
    }

    /// <summary>능력치를 세이브와 잇는다: 마지막에 고른 캐릭터와 그 단계로 시작하고, 바꾸면 세이브에 남긴다.</summary>
    public void Bind(HeroStats stats)
    {
        _stats = stats;
        stats.CharacterChanged += OnCharacterChanged;
        stats.StagesChanged += OnStagesChanged;
        ApplyCharacter();
    }

    /// <summary>세이브를 처음 상태로 지운다(테스트). 조건 없는 캐릭터는 다시 열리며 단계를 새로 굴린다.</summary>
    public void ResetSave() => Core.Data.Reset<MetaSave>();

    private void OnDataReplaced()
    {
        UnlockFreeCharacters();
        ApplyCharacter();
        Changed?.Invoke();
    }

    // 세이브의 캐릭터·단계를 능력치에 넣는다. 캐릭터가 같으면 SetCharacter가 아무 일도 하지 않으므로 단계는 따로 넣는다.
    private void ApplyCharacter()
    {
        if (_stats == null) return;
        SO_HeroCharacter start = FindStartCharacter();
        if (start != null) _stats.SetCharacter(start);
        ApplyStages();
    }

    private void OnCharacterChanged()
    {
        _save.LastCharacter = IdOf(_stats.Character);
        _save.MarkDirty();
        ApplyStages();
        Changed?.Invoke();
    }

    private void OnStagesChanged()
    {
        if (_applying || _stats.Character == null) return;
        CharacterSave save = GetOrCreate(_stats.Character);
        save.Stages = new int[HeroStats.StatCount];
        for (int i = 0; i < save.Stages.Length; i++) save.Stages[i] = _stats.GetStage((HeroStat)i);
        _save.MarkDirty();
    }

    private void ApplyStages()
    {
        if (_stats == null || _stats.Character == null) return;
        _save.Characters.TryGetValue(IdOf(_stats.Character), out CharacterSave save);
        int[] stages = new int[HeroStats.StatCount];
        for (int i = 0; i < stages.Length; i++)
        {
            // 세이브에 없는 칸(처음 고른 잠긴 캐릭터, 나중에 더한 능력치): 열린 캐릭터면 그 칸만 굴리고, 아니면 6단계.
            bool saved = save != null && save.Stages != null && i < save.Stages.Length;
            stages[i] = saved ? save.Stages[i] : save != null && save.Unlocked ? RollStage() : HeroStats.MaxStage;
        }
        _applying = true;
        _stats.LoadStages(stages);
        _applying = false;
        // 굴려서 채운 칸이 있으면 남긴다.
        if (save != null && (save.Stages == null || save.Stages.Length != stages.Length))
        {
            save.Stages = stages;
            _save.MarkDirty();
        }
    }

    // 마지막에 고른 캐릭터가 열려 있으면 그 캐릭터, 아니면 열린 캐릭터 중 첫째(테스트 창으로 고른 잠긴 캐릭터로 시작하지 않게).
    private SO_HeroCharacter FindStartCharacter()
    {
        SO_HeroCharacter firstUnlocked = null;
        for (int i = 0; i < _config.Characters.Count; i++)
        {
            SO_HeroCharacter character = _config.Characters[i];
            if (character == null || !IsUnlocked(character)) continue;
            if (IdOf(character) == _save.LastCharacter) return character;
            if (firstUnlocked == null) firstUnlocked = character;
        }
        return firstUnlocked;
    }

    // 조건이 없는 캐릭터는 처음부터 연다(기획서 12.4). 조건이 있는 캐릭터는 채워도 여기서 열지 않는다(선택 창에서 연다).
    private void UnlockFreeCharacters()
    {
        for (int i = 0; i < _config.Characters.Count; i++)
        {
            SO_HeroCharacter character = _config.Characters[i];
            if (character == null || IsUnlocked(character)) continue;
            if (!_metaConfig.TryGetRule(character, out _)) Unlock(character);
        }
    }

    private CharacterSave GetOrCreate(SO_HeroCharacter character)
    {
        string id = IdOf(character);
        if (!_save.Characters.TryGetValue(id, out CharacterSave save))
        {
            save = new CharacterSave();
            _save.Characters[id] = save;
        }
        return save;
    }

    // 판 밖 굴림(단계·보상)이라 판 결과의 시드 재현(11.2의 3번)과 상관없다.
    private static int RollStage() => UnityEngine.Random.Range(HeroStats.MinStage, HeroStats.MaxStage + 1);
}

/// <summary>판 하나의 보상(기획서 12.4): 클리어한 웨이브 × 보상 주사위 눈의 합.</summary>
public struct RunReward
{
    /// <summary>클리어한 웨이브 수.</summary>
    public int Wave;
    /// <summary>굴린 보상 주사위 눈들(SO_MetaConfig.RewardWavesPerDie 웨이브마다 하나). 비어 있으면 보상 0 — 결과 화면이 굴림을 건너뛴다.</summary>
    public int[] Rolls;
    public int Sum;
    public int Amount;
}

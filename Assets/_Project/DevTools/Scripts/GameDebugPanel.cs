using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 테스트 창(에디터·개발 빌드 전용). "기능" 탭은 테스트 키(GameDebugKeys) 목록을 키 안내와 함께 버튼으로 보이고,
/// 카드 탭에서는 원하는 카드를 눌러 손패에 바로 넣고, "영웅" 탭에서는 캐릭터와 능력치 단계를 바꾸고(로비가 생기기 전까지) 영웅 강화 카드를 바로 적용한다.
/// 카드 종류가 40장을 넘어 종류별 한 장씩(U·K)으로는 원하는 카드를 얻기 어렵고, 한꺼번에 넣으면 적은 장수 기준인 손패에서 고르기도 어려워서 둔다.
/// 게임 UI가 아니라서 프리팹·앵커 규칙 대신 IMGUI로 그린다 — 프리팹·Addressables 등록 없이 코드만으로 붙였다 뗄 수 있게.
/// 화면 높이에 맞춰 통째로 키우고 줄인다(해상도가 달라도 같은 비율로 보이게).
/// F1 또는 왼쪽 위 버튼으로 연다. 창 위를 눌러도 맵에 탭이 함께 들어갈 수 있다(IMGUI는 uGUI 입력을 막지 않는다 — 테스트 도구라 그대로 둔다).
/// </summary>
public sealed class GameDebugPanel : MonoBehaviour
{
    private enum Tab
    {
        Actions,
        Dice,
        Chip,
        Skill,
        Hero,
        Log,
    }

    // 테스트 도구의 배치 값이라 SO로 빼지 않는다(GameDebugKeys의 LongSkip과 같은 이유). 크기는 이 높이의 화면 기준이다.
    private const float ReferenceHeight = 1080f;
    private const float WindowWidth = 560f;
    private const float WindowHeight = 640f;
    private const float ButtonHeight = 40f;
    private const int FontSize = 18;
    private const int Columns = 3;
    private static readonly string[] TabNames = { "기능", "주사위", "칩", "스킬", "영웅", "기록" };
    // 기록 탭에서 웨이브마다 보이는 피해 출처 수(많이 낸 순).
    private const int TopSources = 3;

    private SO_LevelUpConfig _levelUpConfig;
    private SO_DiceConfig _diceConfig;
    private IReadOnlyList<GameDebugKeys.Binding> _bindings;
    private Action<HandCard> _addCard;
    private Action _clearHand;
    private SO_HeroConfig _heroConfig;
    private HeroStats _heroStats;
    private List<SO_HeroCard> _heroCards;
    private MetaProfile _meta;
    private WaveLog _log;
    private readonly int[] _sourceOrder = new int[DamageSource.Count];
    private bool _open;
    private Tab _tab;
    private Vector2 _scroll;
    private Rect _window = new Rect(20f, 80f, WindowWidth, WindowHeight);
    private GUIStyle _button;
    private GUIStyle _label;
    private GUIStyle _wrapLabel;

    /// <param name="bindings">테스트 키 목록(기능 탭).</param>
    /// <param name="addCard">카드 한 장을 손패에 넣는다.</param>
    /// <param name="clearHand">손패를 비운다(끄는 중이던 카드도 내려놓고).</param>
    public void Init(SO_LevelUpConfig levelUpConfig, SO_DiceConfig diceConfig, IReadOnlyList<GameDebugKeys.Binding> bindings,
        Action<HandCard> addCard, Action clearHand)
    {
        _levelUpConfig = levelUpConfig;
        _diceConfig = diceConfig;
        _bindings = bindings;
        _addCard = addCard;
        _clearHand = clearHand;
    }

    /// <param name="heroCards">영웅 강화 카드(누르면 손패를 거치지 않고 바로 적용 — 레벨업에서 고른 것과 같다).</param>
    public void InitHero(SO_HeroConfig heroConfig, HeroStats heroStats, List<SO_HeroCard> heroCards)
    {
        _heroConfig = heroConfig;
        _heroStats = heroStats;
        _heroCards = heroCards;
    }

    /// <param name="meta">세이브(영웅 탭에 재화·기록·열림을 보이고, 재화 더하기·세이브 지우기 버튼을 둔다).</param>
    public void InitMeta(MetaProfile meta) => _meta = meta;

    /// <param name="log">측정 기록("기록" 탭에 웨이브마다 보인다).</param>
    public void InitLog(WaveLog log) => _log = log;

    // 재화 버튼 한 번에 더하는 양(테스트 편의 값이라 SO로 빼지 않는다).
    private const int DebugCurrency = 100;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) _open = !_open;
    }

    private void OnGUI()
    {
        if (_levelUpConfig == null) return;
        float scale = Screen.height / ReferenceHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        if (_button == null) _button = new GUIStyle(GUI.skin.button) { fontSize = FontSize, wordWrap = true };
        if (_label == null) _label = new GUIStyle(GUI.skin.label) { fontSize = FontSize };
        if (_wrapLabel == null) _wrapLabel = new GUIStyle(_label) { wordWrap = true };

        if (GUI.Button(new Rect(20f, 20f, 160f, ButtonHeight), _open ? "카드 닫기 (F1)" : "카드 열기 (F1)", _button)) _open = !_open;
        if (_open) _window = GUI.Window(GetInstanceID(), _window, DrawWindow, "테스트 카드");
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginHorizontal();
        for (int i = 0; i < TabNames.Length; i++)
        {
            GUI.backgroundColor = (int)_tab == i ? Color.cyan : Color.white;
            if (GUILayout.Button(TabNames[i], _button, GUILayout.Height(ButtonHeight))) _tab = (Tab)i;
        }
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("손패 비우기", _button, GUILayout.Height(ButtonHeight))) _clearHand?.Invoke();
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        _scroll = GUILayout.BeginScrollView(_scroll);
        switch (_tab)
        {
            case Tab.Actions:
                DrawActions();
                break;
            case Tab.Dice:
                DrawDice();
                break;
            case Tab.Chip:
                DrawCards(_levelUpConfig.ChipCards);
                break;
            case Tab.Skill:
                DrawCards(_levelUpConfig.SkillCards);
                break;
            case Tab.Hero:
                DrawHero();
                break;
            case Tab.Log:
                DrawLog();
                break;
        }
        GUILayout.EndScrollView();
        GUI.DragWindow();
    }

    // 한 줄에 키 하나: 버튼을 눌러도 키와 같은 일을 한다. 켜고 끄는 기능은 지금 상태를 함께 보인다.
    private void DrawActions()
    {
        GUILayout.Label("F1   이 창 열기/닫기", _label);
        for (int i = 0; i < _bindings.Count; i++)
        {
            GameDebugKeys.Binding binding = _bindings[i];
            string label = $"{binding.KeyName}   {binding.Label}";
            if (binding.IsOn != null)
            {
                bool on = binding.IsOn();
                label += on ? "  [켜짐]" : "  [꺼짐]";
                GUI.backgroundColor = on ? Color.yellow : Color.white;
            }
            if (GUILayout.Button(label, _button, GUILayout.Height(ButtonHeight))) binding.Run?.Invoke();
            GUI.backgroundColor = Color.white;
        }
    }

    // 색 한 줄에 면체를 늘어놓는다. 색·면체 목록은 레벨업 가중치 목록을 그대로 쓴다(가중치 0인 것도 시험할 수 있게 보인다).
    private void DrawDice()
    {
        for (int c = 0; c < _levelUpConfig.ColorWeights.Count; c++)
        {
            DiceColor colors = DiceColors.Normalize(_levelUpConfig.ColorWeights[c].Colors);
            SO_DiceType type = _diceConfig.GetDiceType(colors);
            string name = type != null ? type.DisplayName : colors.ToString();
            GUI.backgroundColor = _diceConfig.GetColor(colors);
            GUILayout.BeginHorizontal();
            for (int f = 0; f < _levelUpConfig.FacesWeights.Count; f++)
            {
                int faces = _levelUpConfig.FacesWeights[f].Faces;
                if (GUILayout.Button($"{name} d{faces}", _button, GUILayout.Height(ButtonHeight)))
                    _addCard?.Invoke(HandCard.Of(new DiceCard(faces, colors)));
            }
            GUILayout.EndHorizontal();
        }
        GUI.backgroundColor = Color.white;
    }

    // 캐릭터 한 줄 + 능력치마다 지금 값(강화 %)과 단계 버튼 1~6 + 영웅 강화 카드.
    private void DrawHero()
    {
        if (_heroStats == null || _heroStats.Character == null) return;

        GUILayout.BeginHorizontal();
        for (int i = 0; i < _heroConfig.Characters.Count; i++)
        {
            SO_HeroCharacter character = _heroConfig.Characters[i];
            if (character == null) continue;
            GUI.backgroundColor = character == _heroStats.Character ? Color.cyan : character.Color;
            // 잠긴 캐릭터도 고를 수 있다(테스트) — 이름에 표시만 한다.
            string lockMark = _meta != null && !_meta.IsUnlocked(character) ? " (잠김)" : string.Empty;
            if (GUILayout.Button(character.DisplayName + lockMark, _button, GUILayout.Height(ButtonHeight))) _heroStats.SetCharacter(character);
        }
        GUILayout.EndHorizontal();
        GUI.backgroundColor = Color.white;
        DrawMeta();
        DrawTrait(_heroStats.Character);

        for (int s = 0; s < HeroStats.StatCount; s++)
        {
            HeroStat stat = (HeroStat)s;
            float bonus = _heroStats.GetBonus(stat);
            string bonusText = bonus > 0f ? $"  (+{bonus * 100f:0.#}%)" : string.Empty;
            GUILayout.Label($"{HeroStatText.GetName(stat)}   {HeroStatText.Format(stat, _heroStats.Get(stat))}{bonusText}", _label);
            GUILayout.BeginHorizontal();
            for (int stage = HeroStats.MinStage; stage <= HeroStats.MaxStage; stage++)
            {
                GUI.backgroundColor = stage == _heroStats.GetStage(stat) ? Color.cyan : Color.white;
                if (GUILayout.Button(stage.ToString(), _button, GUILayout.Height(ButtonHeight))) _heroStats.SetStage(stat, stage);
            }
            GUILayout.EndHorizontal();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Label("영웅 강화 (누르면 바로 적용, 새 판에서 사라짐)", _label);
        int column = 0;
        GUILayout.BeginHorizontal();
        for (int i = 0; i < _heroCards.Count; i++)
        {
            SO_HeroCard card = _heroCards[i];
            if (card == null) continue;
            if (column == Columns)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                column = 0;
            }
            if (GUILayout.Button($"{card.DisplayName} {HeroStatText.FormatAmount(card)}", _button, GUILayout.Height(ButtonHeight))) _heroStats.Apply(card);
            column++;
        }
        GUILayout.EndHorizontal();
        if (GUILayout.Button("강화 비우기", _button, GUILayout.Height(ButtonHeight))) _heroStats.ResetBonuses();
    }

    // 세이브 값 한 줄 + 지금 캐릭터 열기·재화 더하기·세이브 지우기.
    private void DrawMeta()
    {
        if (_meta == null) return;
        GUILayout.Label($"세이브   재화 {_meta.Currency} · 최고 웨이브 {_meta.BestWave} (이 캐릭터 {_meta.GetBestWave(_heroStats.Character)})", _label);
        GUILayout.BeginHorizontal();
        GUI.enabled = !_meta.IsUnlocked(_heroStats.Character);
        if (GUILayout.Button("이 캐릭터 열기(단계 굴림)", _button, GUILayout.Height(ButtonHeight))) _meta.Unlock(_heroStats.Character);
        GUI.enabled = true;
        if (GUILayout.Button($"재화 +{DebugCurrency}", _button, GUILayout.Height(ButtonHeight))) _meta.AddCurrency(DebugCurrency);
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("세이브 지우기", _button, GUILayout.Height(ButtonHeight))) _meta.ResetSave();
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
    }

    // 지금 캐릭터의 특성 이름·설명. 전용 카드가 있으면 손패에 넣는 버튼도 둔다(스킬 탭은 공용 목록만 보인다).
    private void DrawTrait(SO_HeroCharacter character)
    {
        SO_HeroTrait trait = character != null ? character.Trait : null;
        if (trait == null) GUILayout.Label("특성 없음", _label);
        else GUILayout.Label($"특성: {trait.DisplayName} — {CardText.Fill(trait.Description, trait)}", _wrapLabel);
        SO_SkillCard card = character != null ? character.ExclusiveCard : null;
        if (card == null) return;
        GUI.backgroundColor = card.IconColor;
        if (GUILayout.Button($"전용: {card.DisplayName}", _button, GUILayout.Height(ButtonHeight))) _addCard?.Invoke(HandCard.Of(card));
        GUI.backgroundColor = Color.white;
    }

    // 웨이브마다 한 줄(최근 것이 위): 정리 시간, 새어 나간 적, 레벨, 피해 총량과 많이 낸 출처. 자세한 값은 CSV에 있다.
    private void DrawLog()
    {
        if (_log == null) return;
        string path = _log.FilePath;
        GUILayout.Label(path != null ? $"CSV: {path}" : "CSV: 아직 판을 시작하지 않음", _wrapLabel);
        if (path != null && GUILayout.Button("기록 폴더 열기", _button, GUILayout.Height(ButtonHeight)))
            Application.OpenURL("file://" + System.IO.Path.GetDirectoryName(path));

        if (_log.Current != null) DrawRecord(_log.Current, true);
        for (int i = _log.Records.Count - 1; i >= 0; i--) DrawRecord(_log.Records[i], false);
    }

    private void DrawRecord(WaveRecord record, bool running)
    {
        string state = running ? (record.Cleared ? "다 잡음" : "진행 중") : (record.Cleared ? "다 잡음" : "못 잡음");
        float total = record.TotalDamage;
        GUILayout.Label($"W{record.Wave}  {record.Seconds:0.0}초 {state}  처치 {record.Kills}  새어 나감 {record.Leaked} (피해 {record.GoalDamage})  Lv {record.Level}  피해 {total:0}", _label);

        // 많이 낸 순으로 몇 개만. 출처가 12개뿐이라 정렬 없이 매번 가장 큰 것을 고른다.
        for (int i = 0; i < _sourceOrder.Length; i++) _sourceOrder[i] = i;
        string sources = string.Empty;
        for (int n = 0; n < TopSources && total > 0f; n++)
        {
            int best = n;
            for (int i = n + 1; i < _sourceOrder.Length; i++)
                if (record.Damage[_sourceOrder[i]] > record.Damage[_sourceOrder[best]]) best = i;
            (_sourceOrder[n], _sourceOrder[best]) = (_sourceOrder[best], _sourceOrder[n]);
            float damage = record.Damage[_sourceOrder[n]];
            if (damage <= 0f) break;
            sources += $"{DamageSource.NameOf(_sourceOrder[n])} {damage / total * 100f:0}%   ";
        }
        string cards = record.Cards.Count > 0 ? "   카드: " + string.Join(", ", record.Cards) : string.Empty;
        if (sources.Length > 0 || cards.Length > 0) GUILayout.Label("    " + sources + cards, _wrapLabel);
    }

    private void DrawCards<T>(List<T> cards) where T : SO_Card
    {
        int column = 0;
        GUILayout.BeginHorizontal();
        for (int i = 0; i < cards.Count; i++)
        {
            T card = cards[i];
            if (card == null) continue;
            if (column == Columns)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                column = 0;
            }
            GUI.backgroundColor = card.IconColor;
            if (GUILayout.Button(card.DisplayName, _button, GUILayout.Height(ButtonHeight))) AddAsset(card);
            column++;
        }
        GUILayout.EndHorizontal();
        GUI.backgroundColor = Color.white;
    }

    private void AddAsset(SO_Card card)
    {
        if (card is SO_ChipCard || card is SO_SkillCard) _addCard?.Invoke(HandCard.Of(card));
    }
}

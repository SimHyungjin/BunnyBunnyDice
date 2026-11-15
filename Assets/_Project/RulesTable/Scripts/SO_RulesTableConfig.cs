using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 규칙 책상(2026-11-12): 당구대 옆에 놓인 설명용 책상. 테이블 게임 판 옆에 펼쳐 둔 설명서처럼, 판을 하다가 언제든 보러 갈 수 있다.
/// 테스터들이 튜토리얼 글을 거의 안 읽어서, 그림 위주의 네온 화면(합성 · 공격 · 칩)을 책상에 깔아 둔다(실물 진열 안은 기각 — 2026-11-12).
/// 화면 글과 색은 주사위·칩 SO에서 읽는다 — 수치·이름이 바뀌어도 화면이 낡지 않게.
/// 길이 단위는 칸, 높이 0 = 당구대 펠트 윗면.
/// </summary>
[CreateAssetMenu(menuName = "Def/RulesTable/Rules Table Config", fileName = nameof(SO_RulesTableConfig))]
public sealed class SO_RulesTableConfig : SO_Config
{
    [Header("책상 모델")]
    [Tooltip("책상 모델. 피벗 = 상판 윗면 가운데. 비우면 아래 크기·색으로 상자를 쌓아 만든다 — 기본은 이쪽(사용자 2026-11-12: 따로 에셋을 구하지 않아도 된다).")]
    public GameObject DeskPrefab;
    [Tooltip("만든 책상에 입힐 머티리얼(URP Lit). 팔레트 텍스처를 입혀 복제해 쓴다 — 당구대 머티리얼을 그대로 써도 된다.")]
    public Material Material;

    [Header("자리 (당구대 오른쪽)")]
    [Tooltip("당구대 오른쪽 끝에서 책상까지 띄우는 거리. 0 = 당구대에 딱 붙인다.")]
    [Min(0f)] public float Gap = 0f;
    [Tooltip("상판 가로(당구대에서 멀어지는 쪽). 세로는 당구대 세로에 맞춘다 — 한 벌로 붙은 탁자처럼 보이게.")]
    [Min(0.1f)] public float Width = 4f;
    [Tooltip("세로를 당구대에 맞춘 뒤 위아래에서 각각 줄이는 길이 — 당구대 모서리가 둥글어 끝까지 맞추면 책상 모서리가 삐져나와 보인다.")]
    [Min(0f)] public float DepthInset = 0.35f;
    [Tooltip("상판 윗면 높이 — 당구대 맨 윗면(쿠션)에서 얼마나 올리고 내리는지. 0 = 당구대와 같은 높이(사용자 2026-11-12: 내려가 있으면 어색하다).")]
    public float TopOffset = 0f;

    [Header("만든 책상 모양 (DeskPrefab이 비었을 때)")]
    [Min(0.01f)] public float TopThickness = 0.25f;
    [Tooltip("상판 안쪽 펠트 판의 테두리 폭.")]
    [Min(0f)] public float FeltInset = 0.3f;
    [Min(0.01f)] public float LegSize = 0.35f;
    [Tooltip("다리를 상판 모서리에서 안으로 들이는 거리.")]
    [Min(0f)] public float LegInset = 0.35f;
    [Tooltip("상판 윗면 테두리에 깐 네온 줄의 폭(당구대 쿠션 윗면의 줄과 같은 식).")]
    [Min(0.01f)] public float NeonThickness = 0.08f;
    [Tooltip("네온 줄을 상판 바깥 모서리에서 안으로 들이는 거리. 펠트 테두리 폭(FeltInset)보다 작아야 줄이 틀 위에 남는다.")]
    [Min(0f)] public float NeonInset = 0.06f;
    public Color FrameColor = new Color(0.08f, 0.04f, 0.1f);
    public Color FeltColor = new Color(0.16f, 0.05f, 0.2f);
    [Tooltip("네온 띠 색 — 당구대 테두리 발광색(SnookerTable_Arcade_Emission의 204, 4, 69)과 같게.")]
    public Color NeonColor = new Color(0.8f, 0.016f, 0.271f);
    [Tooltip("당구대 머티리얼의 발광 세기(1)와 같게 두면 테두리 밝기가 맞는다.")]
    [Min(0f)] public float NeonIntensity = 1f;

    [Header("규칙 화면 (책상 윗면에 박힌 세로 네온 화면)")]
    [Tooltip("책상 윗면에 까는 화면 UI(월드 공간 캔버스). 탭(합성 · 공격 · 칩)을 누르거나 저절로 넘어갈 때 장이 옆으로 밀린다(사용자 2026-11-13).")]
    public UI_RulesScreen ScreenPrefab;
    [Tooltip("다음 장으로 저절로 넘어가는 간격(초, 실제 시간).")]
    [Min(0.5f)] public float PageInterval = 5f;
    [Tooltip("탭을 누른 뒤 저절로 넘기지 않고 그 장에 머무는 시간(초) — 보려고 눌렀는데 곧 넘어가 버리지 않게.")]
    [Min(0f)] public float ManualHoldTime = 10f;
    [Tooltip("장이 옆으로 밀려 바뀌는 시간(초).")]
    [Min(0.01f)] public float SlideDuration = 0.35f;
    [Tooltip("탭으로 칠 누름의 최대 이동(화면 짧은 변 비율). 넘게 끌면 탭으로 보지 않는다 — 판을 끌다 놓은 손가락이 탭을 누르지 않게.")]
    [Range(0f, 0.2f)] public float TapSlop = 0.03f;
    [Tooltip("화면 먼 쪽을 들어 카메라 쪽으로 기울이는 각도. 0 = 상판에 딱 붙인 화면(사용자 2026-11-12 — 세운 모니터가 아니라 책상 윗면에 박힌 화면).")]
    [Range(0f, 60f)] public float ScreenTilt = 0f;
    [Tooltip("화면을 펠트 위로 띄우는 높이. 딱 붙이되 같은 높이면 면이 겹쳐 깜빡이므로 아주 조금만.")]
    [Min(0f)] public float ScreenLift = 0.005f;
    [Tooltip("화면 밝기(색 배수). 1을 넘겨야 블룸을 받아 네온처럼 빛난다 — 너무 올리면 블룸이 작은 글자를 번지게 한다.")]
    [Min(0f)] public float ScreenBrightness = 1.4f;
    [Tooltip("화면이 있을 때 펠트 판 색 — 화면 바탕(검정에 가까운 남색).")]
    public Color ScreenBackground = new Color(0.02f, 0.027f, 0.047f);

    [Header("규칙 화면 — 탭")]
    [Tooltip("고른 탭 판 색(UI 자홍).")]
    public Color TabActiveColor = new Color(0.961f, 0.02f, 0.424f);
    [Tooltip("고른 탭 글자 색(밝은 판 위 먹색).")]
    public Color TabActiveTextColor = new Color(0.1f, 0.04f, 0.12f);
    [Tooltip("고르지 않은 탭 판 색 — 테두리만 보이게 바탕은 거의 투명.")]
    public Color TabIdleColor = new Color(0.247f, 0.878f, 1f, 0.06f);
    [Tooltip("고르지 않은 탭 글자 색.")]
    public Color TabIdleTextColor = new Color(0.8f, 0.95f, 1f);

    [Header("규칙 화면 — 합성")]
    [Tooltip("얹기 예시: 놓여 있는 주사위. 결과는 얹기 규칙(DiceRules.Stack)으로 계산한다 — 규칙이 바뀌어도 그림이 틀리지 않게.")]
    public ExampleDie MergeBase = new ExampleDie { Colors = DiceColor.Red, Faces = 6, Pips = 4 };
    [Tooltip("얹기 예시: 위에 얹는 주사위(굴린 눈).")]
    public ExampleDie MergeTop = new ExampleDie { Colors = DiceColor.Blue, Faces = 20, Pips = 18 };
    [Tooltip("면체마다의 윤곽 그림(흰색 — 주사위 색으로 물들인다). 없는 면체는 첫 그림을 쓴다.")]
    public List<FaceShape> FaceShapes = new List<FaceShape>();

    [Header("규칙 화면 — 공격")]
    [Tooltip("색마다의 공격 그림. 이 순서로 칸에 놓인다. 이름·색은 주사위 종류 SO(SO_DiceConfig.Types)에서 읽는다.")]
    public List<AttackPicture> Attacks = new List<AttackPicture>();
    [Tooltip("검정(색 없음) 주사위의 그림·이름 색. 검정 주사위 색은 어두운 화면에서 보이지 않는다.")]
    public Color ClearColor = new Color(0.54f, 0.54f, 0.6f);

    [Header("규칙 화면 — 칩")]
    [Tooltip("버프 칸에 놓을 칩(주사위 능력치를 올린다). 이름·색은 칩 SO에서 읽는다.")]
    public List<SO_ChipCard> BuffChips = new List<SO_ChipCard>();
    [Tooltip("디버프 칸에 놓을 칩(맞은 적에게 건다).")]
    public List<SO_ChipCard> DebuffChips = new List<SO_ChipCard>();
    [Tooltip("칩 고리 그림(흰색 — 칩 색으로 물들인다).")]
    public Sprite ChipRing;
    [Tooltip("버프 소제목 색(청록).")]
    public Color BuffColor = new Color(0.247f, 0.878f, 1f);
    [Tooltip("디버프 소제목 색(UI 자홍).")]
    public Color DebuffColor = new Color(0.961f, 0.02f, 0.424f);

    [Serializable]
    public struct ExampleDie
    {
        public DiceColor Colors;
        public int Faces;
        public int Pips;
    }

    [Serializable]
    public struct FaceShape
    {
        public int Faces;
        public Sprite Outline;
    }

    [Serializable]
    public struct AttackPicture
    {
        [Tooltip("이 그림이 보여 줄 주사위 색.")]
        public DiceColor Colors;
        [Tooltip("주사위 색으로 물들일 부분(흰색).")]
        public Sprite Shape;
        [Tooltip("색 그대로 그리는 부분(공격선·적·강조).")]
        public Sprite Mark;
    }

    /// <summary>면체의 윤곽 그림. 없으면 첫 그림.</summary>
    public Sprite GetFaceShape(int faces)
    {
        for (int i = 0; i < FaceShapes.Count; i++)
        {
            if (FaceShapes[i].Faces == faces) return FaceShapes[i].Outline;
        }
        return FaceShapes.Count > 0 ? FaceShapes[0].Outline : null;
    }

    /// <summary>화면에 그릴 주사위 색. 검정은 어두운 화면에서 보이지 않아 ClearColor로 바꾼다.</summary>
    public Color GetDiceColor(SO_DiceConfig dice, DiceColor colors) =>
        DiceColors.Normalize(colors) == DiceColor.Clear ? ClearColor : dice.GetColor(colors);

    public override void SwapNested(Func<SO_Config, SO_Config> swap)
    {
        for (int i = 0; i < BuffChips.Count; i++) BuffChips[i] = (SO_ChipCard)swap(BuffChips[i]);
        for (int i = 0; i < DebuffChips.Count; i++) DebuffChips[i] = (SO_ChipCard)swap(DebuffChips[i]);
    }
}

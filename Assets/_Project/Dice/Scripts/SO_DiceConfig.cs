using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 주사위 설정: 주사위 종류 목록(색 상태마다 SO_DiceType 하나), 놓인 주사위의 외형(면체별 모델)·굴리기 연출·윗면 눈 글자.
/// 종류는 여기서 참조하므로 의존성으로 함께 로드되고, 실행 중 복사본을 만들 때 함께 복사된다(SwapNested).
/// 모든 주사위가 같이 쓰는 규칙(성장 곡선·치명타·각성 기준)은 SO_DiceStatsConfig. 크기는 칸 단위(한 칸 = 1)다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Dice/Dice Config", fileName = nameof(SO_DiceConfig))]
public sealed class SO_DiceConfig : SO_Config
{
    [Header("종류")]
    [Tooltip("색 상태마다 하나(검정 포함 8가지). 빠진 색 상태는 공격하지 않고 이름 '?'·회색으로 보인다.")]
    public List<SO_DiceType> Types = new List<SO_DiceType>();

    [Serializable]
    public struct FaceModel
    {
        [Tooltip("면 수(6·12·20).")]
        public int Faces;
        [Tooltip("카드에 쓰는 면체 이름(육면체 등, 기획서 6.1).")]
        [Localized] public string DisplayName;
        [Tooltip("모델(FBX 그대로 넣어도 된다). 피벗·크기는 상관없다 — 메시 경계를 재서 중심을 맞추고 Size로 키운다.")]
        public GameObject Model;
        [Tooltip("가장 긴 변의 길이(칸). 면체가 클수록 크게(기획서 6.1 작은·중간·큰).")]
        [Min(0.05f)] public float Size;
        [Tooltip("모델을 돌려 놓는 각도. 한 면이 반듯하게 위를 보게(눈 글자가 그 면 위에 놓인다).")]
        public Vector3 Rotation;
        [Tooltip("카드 아이콘. 흰 기반 그림에 주사위 색을 곱해 쓴다.")]
        public Sprite Icon;
        [Tooltip("눈 글자 크기 배율. 면이 작은 면체(d20)는 작게.")]
        [Min(0.1f)] public float PipScale;
    }

    [Header("외형")]
    [Tooltip("면체별 모델. 얹어서 면체가 커지면 모델도 바뀐다.")]
    public List<FaceModel> Models = new List<FaceModel>();
    [Tooltip("모델이 없는 면체에 쓰는 임시 큐브.")]
    public GameObject Prefab;
    [Min(0.05f)] public float PrefabSize = 0.7f;
    [Tooltip("공격선이 나가는 높이(주사위가 선 칸 윗면에서). 주사위 꼭대기 근처.")]
    public float MuzzleHeight = 0.8f;

    [Header("굴리기 연출 (스킬로 다시 굴릴 때 — 놓기·얹기는 아래 솟아나기)")]
    [Tooltip("다시 굴리기·잭팟·올인으로 제자리에서 튀어 굴러 멈추기까지의 시간. 눈은 멈춘 뒤에 보인다.")]
    [Min(0.05f)] public float RollDuration = 0.5f;
    [Tooltip("스킬로 다시 굴릴 때 튀어 오르는 높이(제자리 위).")]
    [Min(0f)] public float StackDropHeight = 0.7f;
    [Tooltip("떨어지는 동안 도는 바퀴 수.")]
    [Min(0f)] public float RollTurns = 2f;
    [Tooltip("전체 시간 중 떨어져 바닥에 닿기까지의 비율. 나머지는 작게 한 번 튀는 시간.")]
    [Range(0.1f, 1f)] public float LandPortion = 0.7f;
    [Tooltip("바닥에 닿은 뒤 한 번 튀는 높이.")]
    [Min(0f)] public float BounceHeight = 0.12f;

    [Header("솟아나기 연출 (영웅이 놓을 때, 기획서 4)")]
    [Tooltip("영웅이 발밑에 놓으면 위에서 떨어지지 않고 바닥에서 톡 솟아난다(뛰어오르는 영웅과 겹치지 않게). 솟아나 멈추기까지의 시간. 눈은 멈춘 뒤에 보인다.")]
    [Min(0.05f)] public float PopDuration = 0.3f;
    [Tooltip("솟아나며 제 크기를 넘었다 돌아오는 정도(ease-out-back 계수). 0이면 넘지 않는다, 1.7이면 약 10% 넘는다.")]
    [Min(0f)] public float PopOvershoot = 1.7f;
    [Tooltip("솟아나는 동안 도는 바퀴 수(굴려서 나온 눈으로 보이게).")]
    [Min(0f)] public float PopTurns = 1f;
    [Tooltip("얹을 때 몸체가 이 크기에서 제 크기로 톡 튀며 커진다(새로 놓을 때는 0에서).")]
    [Range(0f, 1f)] public float StackPopStart = 0.7f;

    [Header("눈 글자")]
    public TMP_FontAsset PipFont;
    [Tooltip("눈 글자 머티리얼. 주사위 색과 겹쳐도 읽히게 외곽선이 있는 것.")]
    public Material PipMaterial;
    public float PipFontSize = 4.2f;
    [Tooltip("눈 글자를 주사위 윗면에서 띄우는 높이 — 면에 묻히지 않게.")]
    public float PipLift = 0.02f;
    public Color PipColor = Color.white;
    [Tooltip("각성(색이 있고 눈이 상한)한 주사위의 눈 색 — 각성을 한눈에 알아보게.")]
    public Color AwakenedPipColor = new Color(1f, 0.82f, 0.2f);

    [Header("각성 표시")]
    [Tooltip("각성한 주사위의 툰 외곽선 색 — 숫자를 읽지 않아도 모양으로 알아보게. 켜져 있는 동안은 이것만 바뀐다(주사위가 많아 계속 빛나면 시끄럽다).")]
    public Color AwakenedOutlineColor = new Color(1f, 0.82f, 0.2f);
    [Tooltip("각성이 켜지는 순간 한 번 퍼지는 고리 색.")]
    public Color AwakenRingColor = new Color(1f, 0.82f, 0.2f);
    [Tooltip("각성 고리가 퍼지는 반지름(칸 = 1).")]
    [Min(0f)] public float AwakenRingRadius = 0.7f;

    /// <summary>면체의 모델. 없으면 임시 큐브(Prefab)를 쓴다.</summary>
    public FaceModel GetModel(int faces)
    {
        for (int i = 0; i < Models.Count; i++)
        {
            if (Models[i].Faces == faces && Models[i].Model != null) return Models[i];
        }
        return new FaceModel { Faces = faces, DisplayName = $"{faces}면체", Model = Prefab, Size = PrefabSize, PipScale = 1f };
    }

    /// <summary>색 상태의 종류. 목록에 없으면 null.</summary>
    public SO_DiceType GetDiceType(DiceColor colors)
    {
        for (int i = 0; i < Types.Count; i++)
        {
            if (Types[i] != null && DiceColors.Normalize(Types[i].Colors) == DiceColors.Normalize(colors)) return Types[i];
        }
        return null;
    }

    public Color GetColor(DiceColor colors)
    {
        SO_DiceType type = GetDiceType(colors);
        return type != null ? type.Color : Color.gray;
    }

    public override void SwapNested(Func<SO_Config, SO_Config> swap)
    {
        for (int i = 0; i < Types.Count; i++) Types[i] = (SO_DiceType)swap(Types[i]);
    }
}

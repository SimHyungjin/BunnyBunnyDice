using UnityEngine;

/// <summary>
/// 판 바깥 환경(당구대). 판 전체가 당구대 펠트 윗면이고, 테이블은 모델 에셋 하나다(펠트 15×8칸 = 맵, 펠트 윗면 y = 0, 피벗 펠트 가운데).
/// 쿠션이 끊긴 자리(네 모서리 + 긴 변 가운데)가 포켓이고, 적은 열린 포켓에서 나온다(기획서 3.3). 열린 포켓은 구멍 안이 빛난다.
/// 길이 단위는 칸이다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Environment/Environment Config", fileName = nameof(SO_EnvironmentConfig))]
public sealed class SO_EnvironmentConfig : SO_Config
{
    [Header("테이블 모델")]
    [Tooltip("당구대 모델. 펠트가 맵 크기(칸)와 같고, 펠트 윗면이 피벗 높이(0)에 있어야 한다. 긴 축 방향은 맵에 맞춰 코드가 돌린다.")]
    public GameObject TablePrefab;
    [Tooltip("모델에 입힐 머티리얼(URP Lit). 비우면 모델 자체 머티리얼을 쓴다. 열린 포켓 빛도 이 머티리얼을 복제해 만든다(발광 셰이더 변형이 빌드에 남도록).")]
    public Material TableMaterial;

    [Header("바닥 카펫 (테이블 밑)")]
    [Tooltip("맵 가운데에서 사방으로 펼칠 거리(칸). 가장 넓은 화면의 가장자리를 넘어야 한다. 0이면 깔지 않는다.")]
    [Min(0f)] public float FloorExtent = 40f;
    [Tooltip("테이블 모델이 없을 때의 바닥 높이. 모델이 있으면 모델 밑면에 깐다.")]
    public float FloorHeight = -4f;
    [Tooltip("무늬 한 칸(마름모 하나)의 크기(칸).")]
    [Min(0.1f)] public float CarpetTileSize = 2f;
    [Min(16)] public int CarpetTextureSize = 256;
    [Tooltip("바탕. 테이블보다 어두워야 시선이 판에 머문다.")]
    public Color CarpetBaseColor = new Color(0.06f, 0.03f, 0.08f);
    [Tooltip("마름모 격자 선(UI 핑크를 어둡게).")]
    public Color CarpetLineColor = new Color(0.22f, 0.05f, 0.13f);
    [Tooltip("마름모 가운데 장식(UI 청록을 어둡게).")]
    public Color CarpetAccentColor = new Color(0.04f, 0.15f, 0.18f);
    [Tooltip("격자 선 반폭(무늬 한 칸 비율).")]
    [Range(0f, 0.2f)] public float CarpetLineWidth = 0.03f;
    [Tooltip("가운데 장식 크기(무늬 한 칸 비율, 마름모 반지름).")]
    [Range(0f, 0.5f)] public float CarpetAccentSize = 0.1f;
    [Range(0f, 1f)] public float CarpetSmoothness = 0.05f;

    [Header("포켓 위치 (모델의 구멍 자리)")]
    [Tooltip("모서리 포켓 중심을 맵 모서리에서 바깥으로 민 거리(x = 맵 긴 축, y = 짧은 축). 모델의 구멍 가운데에 맞춘다.")]
    public Vector2 CornerPocketOffset = new Vector2(0.24f, 0.3f);
    [Tooltip("가운데 포켓 중심을 맵 가장자리에서 바깥으로 민 거리.")]
    public float SidePocketOffset = 0.22f;

    [Header("열린 포켓 빛 (구멍 안의 빛나는 원판)")]
    [Tooltip("열린 포켓(이번 판 출현 구역)을 알리는 빛. 닫힌 포켓은 어둡게 둔다.")]
    public Color PocketGlowColor = new Color(1f, 0.32f, 0.42f);
    [Tooltip("발광 세기(색에 곱한다). 1보다 크면 블룸을 받는다.")]
    [Min(0f)] public float PocketGlowIntensity = 2.5f;
    [Tooltip("보스 포켓(보스가 나오는 포켓, 판 내내) 빛의 색(빨강 대신).")]
    public Color BossGlowColor = new Color(0.7f, 0.35f, 1f);
    [Tooltip("잭팟 보스 웨이브 동안 보스 포켓 빛의 색.")]
    public Color JackpotGlowColor = new Color(1f, 0.78f, 0.15f);
    [Min(0.01f)] public float CornerGlowRadius = 0.3f;
    [Min(0.01f)] public float SideGlowRadius = 0.2f;
    [Tooltip("원판 높이. 구멍 안(펠트 아래)에 둬 구멍 입구로 빛이 보이게 한다.")]
    public float PocketGlowDepth = -0.15f;
    [Min(8)] public int PocketSegments = 32;
}

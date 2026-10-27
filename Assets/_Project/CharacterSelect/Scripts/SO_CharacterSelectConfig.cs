using UnityEngine;

/// <summary>
/// 캐릭터 선택 창 설정(기획서 12.3). 2D 그림이 아직 없어 3D 모델을 찍은 사진을 얼굴로 쓴다(HeroPortraits) — 그 찍는 방식을 둔다.
/// 칸 배치(열 수·칸 비율)는 UI 배치 규칙대로 프리팹(UI_CharacterSelectPopup)에 있다.
/// </summary>
[CreateAssetMenu(menuName = "Def/CharacterSelect/Character Select Config", fileName = nameof(SO_CharacterSelectConfig))]
public sealed class SO_CharacterSelectConfig : SO_Config
{
    [Header("창")]
    [Tooltip("뒤를 덮는 검은 판의 불투명도(Core 기본은 SO_CoreConfig.PopupGuardAlpha). 뒤의 로비 로고·패널이 창과 겹쳐 보이지 않게 진하게 둔다.")]
    [Range(0f, 1f)] public float GuardAlpha = 0.88f;

    [Header("얼굴 사진")]
    [Tooltip("사진 해상도(픽셀). 가로세로 비율은 칸의 사진 자리와 맞춘다 — 다르면 사진이 늘어난다.")]
    public Vector2Int PortraitSize = new Vector2Int(640, 498);
    [Tooltip("담을 범위의 위 끝(키 비율, 1 = 머리끝). 1보다 낮추면 위가 잘린다 — 바니 귀처럼 긴 머리 장식까지 담으면 얼굴이 작아져서 시안처럼 귀 끝은 자른다.")]
    [Range(0.1f, 1f)] public float PortraitTop = 0.86f;
    [Tooltip("담을 범위의 높이(키 비율): 위 끝부터 이만큼 아래까지(0.5 = 상반신).")]
    [Range(0.1f, 1f)] public float PortraitBodyRatio = 0.42f;
    [Tooltip("담을 범위 위아래에 더하는 여백(범위 높이의 비율).")]
    [Range(0f, 1f)] public float PortraitMargin = 0.02f;
    [Range(5f, 60f)] public float PortraitFieldOfView = 20f;
    [Tooltip("모델을 돌리는 각도(0 = 정면). 조금 돌리면 입체감이 산다.")]
    [Range(-90f, 90f)] public float PortraitYaw = 20f;
    [Tooltip("사진을 찍는 동작 자세의 시각(초). 서기 동작의 이 순간을 찍는다.")]
    [Min(0f)] public float PortraitPoseTime = 0f;

    [Header("사진 조명 (찍는 자리에만 닿는다)")]
    [Tooltip("정면 키 라이트 자리(찍는 범위 높이의 배수): x = 카메라 오른쪽, y = 위, z = 카메라 쪽. 장면 조명이 어두워 이것이 없으면 얼굴이 그늘진다.")]
    public Vector3 KeyLightOffset = new Vector3(-0.8f, 0.8f, 2f);
    public Color KeyLightColor = new Color(1f, 0.95f, 0.92f);
    [Min(0f)] public float KeyLightIntensity = 1.2f;
    [Tooltip("뒤쪽 림 라이트 자리. z가 음수면 모델 뒤다 — 윤곽이 배경(칸 바탕)에서 떠오르게.")]
    public Vector3 RimLightOffset = new Vector3(0.9f, 0.6f, -1.2f);
    [Tooltip("윤곽 색. UI 자홍에 맞춘다.")]
    public Color RimLightColor = new Color(1f, 0.3f, 0.6f);
    [Min(0f)] public float RimLightIntensity = 3f;
}

using UnityEngine;

/// <summary>카드를 끌어 주사위를 놓는 조작 설정.</summary>
[CreateAssetMenu(menuName = "Def/Card/Card Drag Config", fileName = nameof(SO_CardDragConfig))]
public sealed class SO_CardDragConfig : SO_Config
{
    [Tooltip("이 거리(HUD 기준 해상도 단위)를 넘게 끌어야 주사위 배치로 바뀐다. 짧게 끌다 놓으면 취소.")]
    public float DeadZone = 80f;
    [Tooltip("카드를 집은 동안의 게임 속도 배율(기획: 50%).")]
    [Range(0.05f, 1f)] public float HoldTimeScale = 0.5f;
}

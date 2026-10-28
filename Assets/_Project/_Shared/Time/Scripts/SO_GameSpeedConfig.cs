using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배속 버튼 설정(HUD 오른쪽 위). 누를 때마다 다음 배율로 넘어가고 끝에서 처음으로 돌아온다.
/// 판이 길어(30웨이브 넘으면 20분 이상) 미로를 다 지은 뒤 지켜보기만 하는 구간을 빨리 넘기게 하려는 것이다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Game/Game Speed Config", fileName = nameof(SO_GameSpeedConfig))]
public sealed class SO_GameSpeedConfig : SO_Config
{
    [Tooltip("버튼을 누를 때마다 차례로 바뀌는 배율. 첫 값이 판 시작 배율이다(새 판마다 첫 값으로 돌아간다).")]
    public List<float> Steps = new List<float> { 1f, 2f, 3f };

    [Header("고정 걸음 (판 시스템)")]
    [Tooltip("판 시스템(OnGameUpdate) 한 걸음의 게임 시간(초). 프레임마다 시간 조각이 다르면 같은 시드라도 판정 순서가 어긋나 판이 재현되지 않는다. "
        + "작을수록 화면이 부드럽고 계산이 많다(배속 × 1/걸음 번 / 초). 0이면 끈다(프레임마다 흐른 시간 그대로).")]
    [Min(0f)] public float FixedStep = 1f / 60f;
    [Tooltip("한 프레임에 도는 걸음 수 상한. 넘는 시간은 버린다(게임이 잠깐 느려질 뿐 결과는 그대로) — 느린 기기에서 밀린 걸음이 쌓여 멈추지 않게. "
        + "배속 × 화면 주사율 ÷ (1/걸음)보다 넉넉해야 배속이 제대로 난다.")]
    [Min(1)] public int MaxStepsPerFrame = 12;
}

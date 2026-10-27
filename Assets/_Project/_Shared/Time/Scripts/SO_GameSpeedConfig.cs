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
}

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 화면의 세이프 에어리어(노치·라운드코너를 피한 영역)를 계산해 알린다.
/// 플랫폼이 알아서 적응한다 — 모바일은 인셋을 반영하고, PC는 전체 화면이 된다.
/// SafeArea 컴포넌트가 화면 변화 시 UpdateSafeArea를 호출해 갱신을 유발한다.
/// </summary>
public sealed class SafeAreaManager : Manager
{
    public struct SafeAreaData
    {
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public float Left, Right, Top, Bottom;
    }

    public event Action<SafeAreaData> OnSafeAreaChanged;
    private SafeAreaData _currentData;

    protected override UniTask OnInitializeAsync()
    {
        UpdateSafeArea();
        return UniTask.CompletedTask;
    }

    public void UpdateSafeArea()
    {
        Rect safeArea = Screen.safeArea;
        float w = Screen.width;
        float h = Screen.height;
        if (w <= 0f || h <= 0f) return;

        Vector2 min = safeArea.position;
        Vector2 max = safeArea.position + safeArea.size;

        min.x /= w; min.y /= h;
        max.x /= w; max.y /= h;

        // 가로가 넓으면 상하 인셋을, 세로가 길면 좌우 인셋을 무시한다 (한 축만 노치 처리). 설정으로 끌 수 있다.
        if (SO_CoreConfig.Instance.SafeAreaIgnoreMinorAxis)
        {
            if (w > h) { min.y = 0f; max.y = 1f; }
            else       { min.x = 0f; max.x = 1f; }
        }

        _currentData = new SafeAreaData
        {
            AnchorMin = min,
            AnchorMax = max,
            Left   = min.x,
            Right  = max.x,
            Top    = max.y,
            Bottom = min.y,
        };

        OnSafeAreaChanged?.Invoke(_currentData);
    }

    public SafeAreaData GetCurrentData() => _currentData;
}

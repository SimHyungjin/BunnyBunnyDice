using System;
using UnityEngine;

/// <summary>
/// 화면 크기가 바뀌면 알린다(기기 회전, 폴더블 접기·펴기, 에디터 Game 뷰 크기 변경).
/// 코어의 SafeArea 갱신은 SafeArea 컴포넌트가 있을 때만 일어나므로, 화면 크기 자체는 여기서 매 프레임 비교한다.
/// </summary>
public sealed class ScreenWatcher : IDisposable
{
    private int _width;
    private int _height;

    public event Action Resized;

    public ScreenWatcher()
    {
        _width = Screen.width;
        _height = Screen.height;
        Core.Loop.OnUpdate += Tick;
    }

    public void Dispose() => Core.Loop.OnUpdate -= Tick;

    private void Tick(float _)
    {
        if (Screen.width == _width && Screen.height == _height) return;
        _width = Screen.width;
        _height = Screen.height;
        Resized?.Invoke();
    }
}

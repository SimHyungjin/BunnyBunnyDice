using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>여러 개가 동시에 뜨는 월드 UI 레이어(이름표·데미지 숫자 등). 레이어 캔버스에 바로 놓는다.</summary>
public sealed class UIManager_WorldLayer
{
    private readonly Canvas _canvas;
    private readonly List<(UI_World Panel, AssetLifetime Life)> _panels = new();

    public UIManager_WorldLayer(Canvas canvas) => _canvas = canvas;

    public async UniTask<T> Show<T>(string key, AssetLifetime life, CancellationToken ct) where T : UI_World
    {
        T instance = await Core.Pool.SpawnAsync<T>(key ?? typeof(T).Name, _canvas.transform, life, ct);
        if (instance == null) return null;

        _panels.Add((instance, life));
        instance.Open();
        return instance;
    }

    public void Close(UI_World instance)
    {
        if (instance == null) return;

        for (int i = _panels.Count - 1; i >= 0; i--)
        {
            if (_panels[i].Panel != instance) continue;
            _panels.RemoveAt(i);
            break;
        }

        instance.Close();
        Core.Pool.Despawn(instance);
    }

    public void CloseAll()
    {
        for (int i = _panels.Count - 1; i >= 0; i--)
            Close(_panels[i].Panel);
        _panels.Clear();
    }

    /// <summary>씬 전환 정리. Scene 수명인 것만 닫는다.</summary>
    public void ClearScene()
    {
        for (int i = _panels.Count - 1; i >= 0; i--)
        {
            (UI_World panel, AssetLifetime life) = _panels[i];
            if (panel == null)
            {
                _panels.RemoveAt(i);
                continue;
            }
            if (life == AssetLifetime.Scene) Close(panel);
        }
    }
}

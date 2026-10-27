using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 한 번에 하나만 뜨는 레이어(HUD·Transition). 새 것을 띄우면 이전 것은 닫힌다.
/// </summary>
public sealed class UIManager_SingleLayer<TPanel> where TPanel : UI_Panel
{
    private readonly Transform _root;
    private readonly int       _order;

    // TPanel이 아니라 UI_Panel로 둔다. 제네릭 타입 매개변수에서는 UnityEngine.Object의 == 오버로드가 쓰이지 않아,
    // 파괴된 인스턴스를 null로 판정하지 못한다.
    private UI_Panel      _current;
    private AssetLifetime _currentLife;

    public UIManager_SingleLayer(Transform root, int order)
    {
        _root  = root;
        _order = order;
    }

    public TPanel Current => _current != null ? (TPanel)_current : null;

    public async UniTask<T> Show<T>(string key, AssetLifetime life, CancellationToken ct) where T : TPanel
    {
        // 새 것을 먼저 띄우고 그다음 이전 것을 닫는다.
        // 닫고 나서 로드하면 그사이 들어온 다른 Show와 둘 다 스폰되어 하나가 닫히지 않은 채 남고,
        // 로드하는 동안 화면이 비기도 한다. 이 순서면 마지막에 끝난 것만 남는다.
        T instance = await Core.Pool.SpawnAsync<T>(key ?? typeof(T).Name, _root, life, ct);
        if (instance == null) return null;

        UIManager.SetupLayerCanvas(instance, _order);

        Close();
        _current = instance;
        _currentLife = life;

        instance.Open();
        return instance;
    }

    public void Close()
    {
        UI_Panel target = _current;
        _current = null;
        if (target == null) return;

        target.Close();
        Core.Pool.Despawn(target);
    }

    /// <summary>씬 전환 정리. Scene 수명이면 닫고, Persistent면 남긴다.</summary>
    public void ClearScene()
    {
        if (_current == null)
        {
            _current = null;   // 파괴된 참조를 비운다
            return;
        }

        if (_currentLife == AssetLifetime.Scene) Close();
    }
}

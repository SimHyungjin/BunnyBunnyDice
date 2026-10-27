using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

/// <summary>에셋을 언제 해제할지에 대한 수명. 코어가 가진 두 스코프 중 하나를 고르는 지름길이다.</summary>
public enum AssetLifetime
{
    /// <summary>씬 전환 시 자동 해제된다. 그 씬에서만 쓰는 에셋. (<see cref="ResourceManager.Scene"/>)</summary>
    Scene,
    /// <summary>씬이 바뀌어도 유지된다. 게임 내내 쓰는 에셋. (<see cref="ResourceManager.Persistent"/>)</summary>
    Persistent,
}

/// <summary>
/// Addressables 에셋의 수명을 <see cref="AssetScope"/> 단위로 관리한다.
///
/// 로드·캐시·해제는 스코프가 하고, 이 매니저는 코어가 쓰는 두 스코프를 들고 있다가
/// 씬이 바뀌면 <see cref="Scene"/> 스코프를 새것으로 갈아 끼운다.
/// 같은 에셋을 두 스코프가 함께 로드해도 Addressables의 참조 카운트가 각자 세므로 서로를 해제하지 않는다.
/// </summary>
public class ResourceManager : Manager
{
    /// <summary>앱 수명 스코프. 해제되지 않는다.</summary>
    public AssetScope Persistent { get; } = new("Persistent");

    /// <summary>현재 씬의 스코프. 씬 전환 시 해제되고 새로 만들어진다 — 참조를 오래 들고 있지 말고 매번 이것으로 접근한다.</summary>
    public AssetScope Scene { get; private set; } = new("Scene");

    protected override async UniTask OnInitializeAsync()
    {
        await Addressables.InitializeAsync().ToUniTask();
    }

    /// <summary>코어가 관리하지 않는 별도 수명의 스코프를 만든다. 다 쓰면 직접 Dispose한다.</summary>
    public AssetScope CreateScope(string name) => new(name);

    public AssetScope GetScope(AssetLifetime life) => life == AssetLifetime.Persistent ? Persistent : Scene;

    #region 지름길 (수명으로 스코프를 고른다)

    /// <summary>키로 에셋을 로드한다. 키를 비우면 타입 이름이 키가 된다.</summary>
    public UniTask<T> LoadAsync<T>(string key = null, AssetLifetime life = AssetLifetime.Scene, CancellationToken ct = default)
        where T : Object
        => GetScope(life).LoadAsync<T>(key, ct);

    /// <summary>라벨에 속한 에셋들을 미리 로드한다.</summary>
    public UniTask PreloadLabelAsync(string label, AssetLifetime life = AssetLifetime.Scene, CancellationToken ct = default)
        => GetScope(life).PreloadLabelAsync(label, ct);

    /// <summary>이미 로드된 에셋을 즉시 꺼낸다(동기). 씬 스코프, 영구 스코프 순으로 찾는다. 없으면 null.</summary>
    public T GetNow<T>(string key = null) where T : Object
    {
        T asset = Scene.GetNow<T>(key);
        return asset != null ? asset : Persistent.GetNow<T>(key);
    }

    #endregion

    /// <summary>씬 전환 시 씬 스코프를 닫고 새로 연다. 그 스코프의 에셋으로 만든 풀도 이때 함께 정리된다.</summary>
    public override void Clear()
    {
        Scene.Dispose();
        Scene = new AssetScope("Scene");
    }
}

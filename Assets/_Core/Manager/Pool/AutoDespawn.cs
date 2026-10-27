using UnityEngine;

/// <summary>
/// 스폰되면 일정 시간 뒤 스스로 풀에 반납되는 오브젝트. (투사체·데칼 등 일회성 오브젝트용)
/// 반납 예약은 PoolManager의 중앙 스케줄러가 처리하므로, 인스턴스마다 타이머를 만들지 않는다.
///
/// 파티클 이펙트라면 파티클 재생·길이 산출까지 해주는 <see cref="ParticleAutoDespawn"/>을 쓴다.
/// </summary>
[DisallowMultipleComponent]
public class AutoDespawn : MonoBehaviour, IPoolable
{
    [SerializeField] protected float duration = 2f;

    [Tooltip("히트스톱 등 Time.timeScale 영향을 무시하고 실제 시간으로 반납할지")]
    [SerializeField] protected bool ignoreTimeScale = false;

    public virtual void OnSpawn()
    {
        App.ScheduleDespawn(gameObject, ResolveDelay(), ignoreTimeScale);
    }

    // 반납 예약은 스폰 세대(Generation) 불일치로 자동 무효화되므로, 여기서 따로 취소할 게 없다.
    public virtual void OnDespawn() { }

    protected virtual float ResolveDelay() => duration > 0f ? duration : 2f;
}

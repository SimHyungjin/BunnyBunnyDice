using UnityEngine;

/// <summary>
/// 파티클 이펙트 전용 자동 반납. 스폰 시 파티클을 재생하고,
/// duration을 비워두면 파티클 설정에서 반납 시점을 자동으로 산출한다.
/// (파티클이 아닌 오브젝트는 <see cref="AutoDespawn"/>을 쓴다 — 자식 탐색 비용이 없다.)
/// </summary>
[DisallowMultipleComponent]
public class ParticleAutoDespawn : AutoDespawn
{
    private const float FallbackDelay = 2f;

    private ParticleSystem[] _particles;

    // 파티클 설정은 런타임에 바뀌지 않는다고 보고 한 번만 계산한다.
    private float _autoDelay;

    private void Awake()
    {
        _particles = GetComponentsInChildren<ParticleSystem>(true);
        _autoDelay = ComputeAutoDelay();
    }

    public override void OnSpawn()
    {
        RestartParticles();
        base.OnSpawn();
    }

    protected override float ResolveDelay() => duration > 0f ? duration : _autoDelay;

    /// <summary>
    /// 모든 파티클이 끝나는 시점. 자식 파티클까지 보고, 그중 가장 늦게 끝나는 것을 고른다.
    ///
    /// 한 파티클 시스템이 끝나는 시점은 "지연 + 방출 기간 + 마지막에 방출된 입자의 수명"이다.
    /// 수명을 빼면 마지막 입자가 사라지기 전에 반납되어 이펙트가 잘린다.
    /// 시뮬레이션 속도가 빠르면 그만큼 일찍 끝난다.
    /// </summary>
    private float ComputeAutoDelay()
    {
        if (_particles == null || _particles.Length == 0) return FallbackDelay;

        float longest = 0f;
        for (int i = 0; i < _particles.Length; i++)
        {
            ParticleSystem.MainModule main = _particles[i].main;

            if (main.loop)
            {
                Debug.LogWarning(
                    $"[ParticleAutoDespawn] '{name}'에 루프 파티클('{_particles[i].name}')이 있어 끝나는 시점을 알 수 없습니다. " +
                    $"duration을 직접 지정하세요. 지금은 {FallbackDelay}초 뒤 반납합니다.", this);
                return FallbackDelay;
            }

            float speed = main.simulationSpeed > 0f ? main.simulationSpeed : 1f;
            float end = (MaxOf(main.startDelay) + main.duration + MaxOf(main.startLifetime)) / speed;
            if (end > longest) longest = end;
        }

        return longest > 0f ? longest : FallbackDelay;
    }

    // MinMaxCurve가 낼 수 있는 최댓값. 커브 모드는 0~1로 정규화된 커브에 배수를 곱하므로 배수가 상한이다.
    private static float MaxOf(ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:     return curve.constant;
            case ParticleSystemCurveMode.TwoConstants: return curve.constantMax;
            default:                                   return curve.curveMultiplier;
        }
    }

    private void RestartParticles()
    {
        if (_particles == null) return;

        for (int i = 0; i < _particles.Length; i++)
        {
            ParticleSystem p = _particles[i];
            if (p == null) continue;
            p.Clear(true);
            p.Play(true);
        }
    }
}

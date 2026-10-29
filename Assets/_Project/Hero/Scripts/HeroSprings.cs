using UnityEngine;
using UniVRM10;

/// <summary>
/// 모델의 흔들림 뼈(VRM 스프링 본 — 머리카락·옷)를 모델 크기에 맞춘다. 스프링 본은 뼈 길이를 만들 때의 크기로 고정해 두어,
/// 연출이 모델을 키우면(로비에서 판 위 크기의 몇 배로 앉아 있다) 실제 뼈보다 짧은 길이로 끌어당겨 쉬지 않고 휘날렸다(2026-10-29).
/// 크기가 정한 비율 넘게 바뀌면 지금 크기로 다시 만든다 — 뛰어드는 동안처럼 크기가 계속 바뀌어도 매 프레임 만들지 않게.
/// VRM 모델이 아니면 아무 일도 하지 않는다.
/// </summary>
public sealed class HeroSprings
{
    private readonly Vrm10Instance _vrm;
    // 마지막으로 다시 만든 크기. 처음엔 언제 만들어졌는지 모르므로 첫 맞춤에서 한 번 만든다.
    private float _builtScale = -1f;

    public HeroSprings(GameObject model)
    {
        _vrm = model.GetComponentInChildren<Vrm10Instance>();
    }

    /// <param name="step">지난번 크기에서 이 비율 넘게 바뀌면 다시 만든다(SO_HeroConfig.SpringRebuildScaleStep).</param>
    public void Fit(float step)
    {
        if (_vrm == null || _vrm.Runtime == null) return;
        float scale = _vrm.transform.lossyScale.x;
        if (_builtScale > 0f && Mathf.Abs(scale / _builtScale - 1f) <= step) return;
        if (_vrm.Runtime.SpringBone.ReconstructSpringBone()) _builtScale = scale;
    }
}

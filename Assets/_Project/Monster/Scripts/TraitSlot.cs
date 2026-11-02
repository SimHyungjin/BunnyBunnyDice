/// <summary>
/// 몬스터에 붙은 특성 하나와 그 세기. 세기 1이 특성 SO 값 그대로이고, 보조 특성(웨이브 특성에 더 붙는 무작위 특성)은 낮춰 받는다
/// — 한 웨이브의 주인공 특성이 뚜렷하게 읽히게(2026-11-02 사용자). 어떤 값이 세기를 받는지는 MonsterKind가 정한다.
/// </summary>
public readonly struct TraitSlot
{
    public readonly SO_MonsterTrait Trait;
    public readonly float Strength;

    public TraitSlot(SO_MonsterTrait trait, float strength = 1f)
    {
        Trait = trait;
        Strength = strength;
    }
}

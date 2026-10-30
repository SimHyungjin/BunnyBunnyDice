/// <summary>갈고리: 모든 칩 효과의 세기 배율(칩 리더). 특성 등 바깥에서 구현한다 — 칩 쪽은 누가 구현하는지 모른다.</summary>
public interface IChipScale
{
    /// <summary>칩 효과에 곱할 세기(1 = 그대로). 디버프는 세기(얼림은 어는 시간), 강화는 늘리는 비율에 곱한다. 지속 시간은 그대로다.</summary>
    float ChipStrength { get; }
}

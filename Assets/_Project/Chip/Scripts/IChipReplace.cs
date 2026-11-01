/// <summary>
/// 갈고리: 칩이 있는 칸에 칩을 놓아 바꿀 수 있다(칩 리더). 칩은 원래 바꿀 수 없다(기획서 7) — 이 갈고리가 있을 때만 예외다.
/// 특성 등 바깥에서 구현한다 — 칩 쪽은 누가 구현하는지 모르고, 있는지만 묻는다(ChipField.CanReplace).
/// </summary>
public interface IChipReplace
{
}

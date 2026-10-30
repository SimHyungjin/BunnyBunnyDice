/// <summary>
/// 갈고리: 레이즈 배율을 바꾼다(하이 스테이크). 특성 등 바깥에서 구현한다 — 레이즈 쪽은 누가 구현하는지 모른다.
/// 레이즈 카드·영웅이 올라탄 주사위·핫 스트릭이 모두 같은 배율(SO_SkillCard_Raise.GetScales)을 지난다.
/// </summary>
public interface IRaiseScale
{
    /// <summary>기본 배율(레이즈 카드 값)을 받아 쓸 배율로 바꾼다.</summary>
    void Scale(ref float damageScale, ref float speedScale);
}

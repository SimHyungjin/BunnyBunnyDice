using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 코어 점검용 팝업. 이것 하나를 여는 것만으로 여러 계통이 한 번에 검증된다
/// — Addressables 로드 → ResourceManager → PoolManager 스폰 → UIManager 팝업 레이어 →
///   클릭 가드 패널 → CoreTween 등장/퇴장 연출.
/// </summary>
public sealed class UI_TestPopup : UI_Popup
{
    /// <summary>
    /// 다음에 열릴 팝업이 쓸 연출. 정적으로 두는 이유가 있다 —
    /// <c>App.ShowPopup</c>이 스폰과 동시에 <see cref="UI_Popup.Open"/>을 부르므로,
    /// 스폰 결과를 받은 뒤에 AnimationType을 바꾸면 이미 한 번 재생된 뒤다.
    /// 그 상태에서 다시 재생시키면 연출이 두 번 겹쳐 돌아 결과를 신뢰할 수 없다.
    /// </summary>
    public static UIAnimationType NextAnimation = UIAnimationType.Scale;

    [SerializeField] private Text _label;

    public override void Open()
    {
        AnimationType = NextAnimation;
        base.Open();
    }

    /// <summary>화면에 표시할 문구. 프리팹에 Text가 없으면 조용히 무시한다.</summary>
    public void SetLabel(string text)
    {
        if (_label == null) _label = GetComponentInChildren<Text>(true);
        if (_label != null) _label.text = text;
    }
}

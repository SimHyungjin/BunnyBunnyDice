using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

public class UI_Popup : UI_Panel
{
    public UIAnimationType AnimationType = UIAnimationType.None;

    [Tooltip("뒤로가기(Android 뒤로가기 키·ESC)로 닫힐지. 끄면 뒤로가기는 이 팝업에 막혀 아무 일도 하지 않는다. (필수 동의·진행 중 확인창 등)")]
    public bool CloseOnBack = true;

    internal Action OnCloseRequest;

    public override void Open()
    {
        base.Open();
        this.StopAnimation();
        this.PlayIn(AnimationType).Forget();
    }

    public override void Close()
    {
        if (!_isOpened) return;
        _isOpened = false;
        CloseTask().Forget();
    }

    private async UniTask CloseTask()
    {
        this.StopAnimation();

        // 닫기 연출이 끊겼다면(닫히는 중에 다시 열렸거나 반납됐다) 닫기 처리를 하지 않는다.
        // 그대로 진행하면 방금 다시 연 팝업을 레이어가 닫아 버린다.
        if (!await this.PlayOut(AnimationType)) return;

        OnCloseEvent?.Invoke();
        OnCloseRequest?.Invoke();
    }

    protected override void OnDespawned() => OnCloseRequest = null;
}

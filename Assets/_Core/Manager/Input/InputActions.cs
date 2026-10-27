using UnityEngine;

/// <summary>
/// 입력 액션 모듈의 베이스. 기기 하나(키보드·패드·터치 등)를 담당한다.
/// 상속해서 Connect/Disconnect/OnUpdate를 구현하고, 기기에서 읽은 값을 게임의 입력 상태 클래스에 쓴다(<see cref="ButtonInput"/> 참고).
/// App.SetInput&lt;T&gt;()으로 활성화한다.
/// </summary>
public abstract class InputActions
{
    protected InputManager Manager;
    protected Camera MainCamera => Camera.main;

    /// <summary>
    /// 씬이 바뀌어도 유지할지. 뒤로가기처럼 앱 전체에서 늘 살아 있어야 하는 입력이 켠다.
    /// 유지되는 모듈은 SetInput·RemoveAllInputs로 바뀌지 않고, RemoveInput으로 직접 빼야 꺼진다.
    /// </summary>
    public virtual bool Persistent => false;

    private bool _initialized;

    public void Init(InputManager manager)
    {
        if (_initialized) return;
        Manager = manager;
        _initialized = true;
    }

    /// <summary>활성화될 때. Unity Input System 콜백 구독 등을 여기서.</summary>
    public abstract void Connect();

    /// <summary>비활성화될 때. 구독 해제.</summary>
    public abstract void Disconnect();

    /// <summary>활성 상태 동안 매 프레임 호출된다.</summary>
    public abstract void OnUpdate(float deltaTime);
}

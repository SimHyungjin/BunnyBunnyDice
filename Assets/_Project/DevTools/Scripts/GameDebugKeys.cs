using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 테스트용 단축키(에디터·개발 빌드 전용). 키·이름·할 일을 한 목록(Binding)으로 들고, 키를 누르면 그 일을 한다.
/// 테스트 창(GameDebugPanel)도 같은 목록을 버튼과 키 안내로 보인다 — 키를 더하면 창에도 따라 나오게(안내가 실제 키와 어긋나지 않게).
/// 무엇을 할지는 만드는 쪽(GameScene)이 Add로 넣는다.
/// </summary>
public sealed class GameDebugKeys : MonoBehaviour
{
    public sealed class Binding
    {
        public Key Key;
        /// <summary>Shift와 함께 눌러야 하는지. 같은 키라도 Shift 여부가 다르면 다른 기능이다(N / Shift+N).</summary>
        public bool Shift;
        public string Label;
        public Action Run;
        /// <summary>켜고 끄는 기능이면 지금 켜져 있는지. 아니면 null.</summary>
        public Func<bool> IsOn;

        public string KeyName => Shift ? "Shift+" + Key : Key.ToString();
    }

    private readonly List<Binding> _bindings = new List<Binding>();

    public IReadOnlyList<Binding> Bindings => _bindings;

    /// <param name="isOn">켜고 끄는 기능이면 지금 상태. 창에 켜짐/꺼짐으로 보인다.</param>
    public void Add(Key key, bool shift, string label, Action run, Func<bool> isOn = null)
    {
        _bindings.Add(new Binding { Key = key, Shift = shift, Label = label, Run = run, IsOn = isOn });
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        bool shift = keyboard.shiftKey.isPressed;
        for (int i = 0; i < _bindings.Count; i++)
        {
            Binding binding = _bindings[i];
            if (binding.Shift == shift && keyboard[binding.Key].wasPressedThisFrame) binding.Run?.Invoke();
        }
    }
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 피해 숫자 표시. 맞은 자리에서 분수처럼 무작위 좌우로 튀어 올랐다가 떨어지며 사라진다.
/// 올라갈 때는 세로로 늘고 떨어질 때는 가로로 퍼져 탄력 있게 보이게 한다(늘었다 찌그러지기).
/// 숫자는 늘 카메라를 정면으로 보고, 좌우는 화면 기준이다 — 세로 화면에서 카메라가 돌아도 같은 모양으로 튄다.
/// 글자 오브젝트는 재사용한다(AttackEffects와 같은 이유).
/// </summary>
public sealed class DamageNumbers : IDisposable
{
    private struct Number
    {
        public TextMeshPro Text;
        public Color Color;
        /// <summary>뜬 자리(맵 로컬). 아래 Side·Height는 여기서 화면 좌우·위로 벗어난 양이다.</summary>
        public Vector3 Origin;
        public float Side;
        public float Height;
        public float SideVelocity;
        public float UpVelocity;
        public float LaunchSpeed;
        public float BaseScale;
        public float Age;
    }

    private readonly SO_DamageNumberConfig _config;
    private readonly Func<Camera> _getCamera;
    private readonly Transform _root;
    private readonly List<Number> _active = new List<Number>();
    private readonly Stack<TextMeshPro> _idle = new Stack<TextMeshPro>();

    public DamageNumbers(Transform parent, SO_DamageNumberConfig config, Func<Camera> getCamera)
    {
        _config = config;
        _getCamera = getCamera;
        _root = new GameObject("DamageNumbers").transform;
        _root.SetParent(parent, false);
        for (int i = 0; i < config.InitialCapacity; i++) _idle.Push(Create());
        Core.Loop.OnLateUpdate += LateTick;
    }

    public void Dispose() => Core.Loop.OnLateUpdate -= LateTick;

    /// <param name="position">맞은 몬스터의 맵 로컬 (x, z).</param>
    /// <param name="ground">그 자리 바닥 윗면 높이.</param>
    public void Show(Vector2 position, float ground, float amount, DamageKind kind)
    {
        if (!_config.Enabled) return;

        TextMeshPro text = _idle.Count > 0 ? _idle.Pop() : Create();
        text.gameObject.SetActive(true);
        text.font = _config.Font;
        if (_config.Material != null) text.fontSharedMaterial = _config.Material;
        text.fontSize = _config.FontSize;
        // 피해는 정수로 들어온다(MonsterField.ApplyDamage).
        text.text = kind == DamageKind.BonusExp ? "+" + Mathf.RoundToInt(amount) : Mathf.RoundToInt(amount).ToString();

        Color color = kind == DamageKind.BonusExp ? _config.BonusExpColor
            : kind == DamageKind.Critical ? _config.CriticalColor
            : kind == DamageKind.Reduced ? _config.ReducedColor
            : kind == DamageKind.Poison ? _config.PoisonColor
            : kind == DamageKind.Fire ? _config.FireColor : _config.NormalColor;
        text.color = color;
        text.transform.localScale = Vector3.zero;

        float launch = UnityEngine.Random.Range(_config.LaunchSpeed.x, _config.LaunchSpeed.y);
        _active.Add(new Number
        {
            Text = text,
            Color = color,
            Origin = new Vector3(position.x, ground + _config.StartHeight, position.y),
            SideVelocity = UnityEngine.Random.Range(-_config.SideSpeed, _config.SideSpeed),
            UpVelocity = launch,
            LaunchSpeed = launch,
            BaseScale = kind == DamageKind.Critical || kind == DamageKind.BonusExp ? _config.CriticalScale : 1f,
        });
    }

    public void Clear()
    {
        for (int i = 0; i < _active.Count; i++) Release(_active[i].Text);
        _active.Clear();
    }

    // 게임 시간을 따른다 — 카드를 집는 동안 공격도 느려지므로 숫자도 같은 빠르기로 흐른다.
    private void LateTick(float _)
    {
        if (_active.Count == 0) return;
        float deltaTime = Time.deltaTime;
        Camera camera = _getCamera();
        Quaternion facing = camera != null ? camera.transform.rotation : Quaternion.identity;
        // 화면 좌우를 맵 로컬로 옮긴다(맵 루트가 돌아 있어도 화면 기준으로 튀게).
        Vector3 right = _root.InverseTransformDirection(facing * Vector3.right);
        Vector3 up = _root.InverseTransformDirection(Vector3.up);

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Number number = _active[i];
            number.Age += deltaTime;
            if (number.Age >= _config.Duration)
            {
                Release(number.Text);
                _active[i] = _active[_active.Count - 1];
                _active.RemoveAt(_active.Count - 1);
                continue;
            }

            number.UpVelocity -= _config.Gravity * deltaTime;
            number.Side += number.SideVelocity * deltaTime;
            number.Height += number.UpVelocity * deltaTime;

            Transform t = number.Text.transform;
            t.localPosition = number.Origin + right * number.Side + up * number.Height;
            t.rotation = facing;

            // 세로 속도로 모양을 바꾼다: 위로 빠를수록 세로로 길고 가로로 좁게, 아래로 빠를수록 반대로.
            float stretch = Mathf.Clamp(number.UpVelocity / number.LaunchSpeed, -1f, 1f) * _config.Stretch;
            float pop = Mathf.Clamp01(number.Age / _config.PopDuration);
            float scale = number.BaseScale * pop;
            t.localScale = new Vector3(scale * (1f - stretch), scale * (1f + stretch), scale);

            Color color = number.Color;
            float remaining = (_config.Duration - number.Age) / _config.Duration;
            color.a = Mathf.Clamp01(remaining / _config.FadeRatio);
            number.Text.color = color;
            _active[i] = number;
        }
    }

    private TextMeshPro Create()
    {
        GameObject go = new GameObject("DamageNumber");
        go.transform.SetParent(_root, false);
        go.SetActive(false);
        TextMeshPro text = go.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(2f, 1f);
        return text;
    }

    private void Release(TextMeshPro text)
    {
        text.gameObject.SetActive(false);
        _idle.Push(text);
    }
}

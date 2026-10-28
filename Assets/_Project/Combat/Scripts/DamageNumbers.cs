using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 피해 숫자 표시. 맞은 자리에서 분수처럼 무작위 좌우로 튀어 올랐다가 떨어지며 사라진다.
/// 올라갈 때는 세로로 늘고 떨어질 때는 가로로 퍼져 탄력 있게 보이게 한다(늘었다 찌그러지기).
/// 숫자는 늘 카메라를 정면으로 보고, 좌우는 화면 기준이다 — 세로 화면에서 카메라가 돌아도 같은 모양으로 튄다.
///
/// 모든 숫자를 메시 하나에 담아 매 프레임 다시 채운다(MonsterHealthBars와 같은 방식). 숫자마다 TextMeshPro를 두면
/// 뜰 때마다 글자 메시를 새로 만들고 숫자마다 그리기가 하나씩 붙어, 후반(1초에 수백 번 맞음)에 50웨이브 25fps까지 떨어졌다
/// (끄면 100fps — 2026-10-28). 개수를 막으면 숫자가 계속 갈려 읽히지 않으므로 개수는 막지 않는다(사용자).
/// 글자 모양은 TMP 폰트 아틀라스에서 숫자·"+" 글자 자리만 읽어 쓰고, 머티리얼은 TMP SDF 그대로다 — 외곽선·모양이 TMP와 같다.
/// 그래서 TMP가 SDF 셰이더에 넘기는 정점 값(아틀라스 UV, uv.w = SDF 배율, 카메라 쪽 법선)을 같은 식으로 채운다.
/// 정점은 배열 하나(Vertex)에 바로 쓰고, 삼각형 번호는 사각형 순서가 늘 같으므로 미리 만들어 두고 쓸 만큼만 그린다
/// — 목록 다섯 개에 하나씩 더해 올리던 방식은 숫자를 모은 시험에서 프레임당 6ms였다(2026-10-28 프로파일).
/// </summary>
public sealed class DamageNumbers : IDisposable
{
    private struct Number
    {
        public int Value;
        /// <summary>앞에 "+"를 붙이는지(더 받은 경험치).</summary>
        public bool Plus;
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

    // 글자 하나의 모양(폰트 단위 — 폰트 크기 배율을 곱하기 전). 패딩(외곽선이 번질 자리)까지 넣은 사각형이다.
    private struct Glyph
    {
        public bool Valid;
        public float Left, Right, Bottom, Top;
        public float Advance;
        /// <summary>글자마다 다른 크기 배율(character.scale × glyph.scale). 폰트 크기 배율에 곱한다.</summary>
        public float Scale;
        public Vector2 UvMin, UvMax;
    }

    // 정점 하나. 순서는 Unity 정점 속성 순서(위치·법선·색·UV0)와 같아야 한다 — Layout과 짝.
    [StructLayout(LayoutKind.Sequential)]
    private struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Color32 Color;
        public Vector4 Uv;
    }

    private static readonly VertexAttributeDescriptor[] Layout =
    {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
        new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
        new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
    };

    // 메시를 올릴 때 검사·경계 계산을 건너뛴다 — 번호는 미리 만든 그대로고, 경계는 넉넉히 한 번 정해 둔다.
    private const MeshUpdateFlags UpdateFlags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds
        | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontResetBoneBounds;

    // 숫자는 판 위에서만 뜨므로 판보다 넉넉한 경계면 화면 밖으로 잘리지 않는다(맵 로컬, 칸 단위).
    private static readonly Bounds MeshBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);

    // 쓰는 글자: 0~9와 "+".
    private const string Characters = "0123456789+";
    private const int PlusIndex = 10;
    // int 최대 10자리 + "+".
    private const int MaxDigits = 11;

    private readonly SO_DamageNumberConfig _config;
    private readonly Func<Camera> _getCamera;
    private readonly Transform _root;
    private readonly Mesh _mesh;
    private readonly MeshRenderer _renderer;
    private readonly List<Number> _active = new List<Number>();
    private readonly Glyph[] _glyphs = new Glyph[Characters.Length];
    private readonly int[] _digits = new int[MaxDigits];
    // 사각형 몇 개 몫을 잡아 두었는지. 모자라면 두 배로 늘린다(줄이지 않는다).
    private int _quadCapacity;
    private NativeArray<Vertex> _vertexData;
    private int _quadCount;
    // 글자 모양을 읽어 둔 폰트·머티리얼. 실행 중 설정에서 바꾸면 다시 읽는다.
    private TMP_FontAsset _cachedFont;
    private Material _cachedMaterial;
    private float _ascent;
    private float _descent;

    public DamageNumbers(Transform parent, SO_DamageNumberConfig config, Func<Camera> getCamera)
    {
        _config = config;
        _getCamera = getCamera;
        GameObject go = new GameObject("DamageNumbers");
        _root = go.transform;
        _root.SetParent(parent, false);
        _mesh = new Mesh { name = "DamageNumbers" };
        _mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = _mesh;
        _renderer = go.AddComponent<MeshRenderer>();
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        // 체력바(MonsterHealthBars, 2) 뒤, 다른 투명 연출(공격선 등, 0) 앞에 그린다 — 숫자가 많아도 체력이 가려지지 않게(2026-10-28 사용자).
        // 둘 다 판 전체를 덮는 투명 메시 하나라, 거리로 정렬하면 기준점(메시 가운데)이 프레임마다 엇갈려 앞뒤가 깜빡인다.
        _renderer.sortingOrder = 1;
        _renderer.enabled = false;
        _active.Capacity = config.InitialCapacity;
        Core.Loop.OnGameUpdate += Tick;
        Core.Loop.OnLateUpdate += LateTick;
    }

    public void Dispose()
    {
        Core.Loop.OnGameUpdate -= Tick;
        Core.Loop.OnLateUpdate -= LateTick;
        UnityEngine.Object.Destroy(_mesh);
        if (_vertexData.IsCreated) _vertexData.Dispose();
    }

    /// <param name="position">맞은 몬스터의 맵 로컬 (x, z).</param>
    /// <param name="ground">그 자리 바닥 윗면 높이.</param>
    public void Show(Vector2 position, float ground, float amount, DamageKind kind)
    {
        if (!_config.Enabled) return;

        Color color = kind == DamageKind.BonusExp ? _config.BonusExpColor
            : kind == DamageKind.Critical ? _config.CriticalColor
            : kind == DamageKind.Reduced ? _config.ReducedColor
            : kind == DamageKind.Poison ? _config.PoisonColor
            : kind == DamageKind.Fire ? _config.FireColor : _config.NormalColor;

        float launch = UnityEngine.Random.Range(_config.LaunchSpeed.x, _config.LaunchSpeed.y);
        _active.Add(new Number
        {
            // 피해는 정수로 들어온다(MonsterField.ApplyDamage).
            Value = Mathf.Max(Mathf.RoundToInt(amount), 0),
            Plus = kind == DamageKind.BonusExp,
            Color = color,
            Origin = new Vector3(position.x, ground + _config.StartHeight, position.y),
            SideVelocity = UnityEngine.Random.Range(-_config.SideSpeed, _config.SideSpeed),
            UpVelocity = launch,
            LaunchSpeed = launch,
            BaseScale = kind == DamageKind.Critical || kind == DamageKind.BonusExp ? _config.CriticalScale : 1f,
        });
    }

    public void Clear() => _active.Clear();

    // 게임 시간을 따른다 — 카드를 집는 동안 공격도 느려지므로 숫자도 같은 빠르기로 흐른다.
    // 배속에서도 화면에 남는 숫자 수가 늘지 않고, 멈추면 맞은 순간 그대로 멈춘다.
    private void Tick(float deltaTime)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Number number = _active[i];
            number.Age += deltaTime;
            if (number.Age >= _config.Duration)
            {
                // 순서는 상관없으므로 마지막 것을 옮겨 와 지운다.
                _active[i] = _active[_active.Count - 1];
                _active.RemoveAt(_active.Count - 1);
                continue;
            }

            number.UpVelocity -= _config.Gravity * deltaTime;
            number.Side += number.SideVelocity * deltaTime;
            number.Height += number.UpVelocity * deltaTime;
            _active[i] = number;
        }
    }

    // 채우는 것은 카메라가 움직인 뒤에 한다 — 숫자는 늘 카메라를 정면으로 본다.
    private void LateTick(float _)
    {
        Material material = _config.Material != null ? _config.Material : _config.Font != null ? _config.Font.material : null;
        // 비어 있으면 렌더러만 끈다(빈 메시를 올리지 않게).
        _renderer.enabled = _active.Count > 0 && _config.Font != null && material != null;
        if (!_renderer.enabled) return;
        if (_config.Font != _cachedFont || material != _cachedMaterial) CacheGlyphs(_config.Font, material);
        _renderer.sharedMaterial = material;

        Camera camera = _getCamera();
        Quaternion facing = camera != null ? camera.transform.rotation : Quaternion.identity;
        // 화면 좌우·위를 맵 로컬로 옮긴다(맵 루트가 돌아 있어도 화면 기준으로 튀게).
        // 튀는 방향은 세상 위, 글자를 세우는 방향은 화면 위다(TMP를 카메라 회전으로 돌렸을 때와 같다).
        Vector3 right = _root.InverseTransformDirection(facing * Vector3.right);
        Vector3 up = _root.InverseTransformDirection(Vector3.up);
        Vector3 screenUp = _root.InverseTransformDirection(facing * Vector3.up);
        // TMP 글자 메시의 법선(로컬 -z)을 카메라 회전으로 돌린 것 — SDF 셰이더가 비스듬히 볼 때 선명도를 맞추는 데 쓴다.
        Vector3 normal = _root.InverseTransformDirection(facing * Vector3.back);
        // TMP의 3D 글자 배율(원근 카메라는 0.1을 곱한다).
        float fontScale = _config.FontSize / _cachedFont.faceInfo.pointSize * _cachedFont.faceInfo.scale * 0.1f;
        float lossy = Mathf.Abs(_root.lossyScale.y);

        // 글자 수를 먼저 세어 자리를 잡는다(정점 배열은 쓰는 도중에 늘릴 수 없다).
        int quads = 0;
        for (int i = 0; i < _active.Count; i++) quads += CharacterCount(_active[i]);
        EnsureCapacity(quads);
        _quadCount = 0;

        for (int i = 0; i < _active.Count; i++)
        {
            Number number = _active[i];
            // 세로 속도로 모양을 바꾼다: 위로 빠를수록 세로로 길고 가로로 좁게, 아래로 빠를수록 반대로.
            float stretch = Mathf.Clamp(number.UpVelocity / number.LaunchSpeed, -1f, 1f) * _config.Stretch;
            float pop = Mathf.Clamp01(number.Age / _config.PopDuration);
            float scale = number.BaseScale * pop;
            if (scale <= 0f) continue;

            float remaining = (_config.Duration - number.Age) / _config.Duration;
            Color color = number.Color;
            color.a *= Mathf.Clamp01(remaining / _config.FadeRatio);

            Vector3 center = number.Origin + right * number.Side + up * number.Height;
            AppendNumber(number, center, right * (scale * (1f - stretch)), screenUp * (scale * (1f + stretch)),
                fontScale, fontScale * lossy * scale * (1f + stretch), color, normal);
        }

        _mesh.SetVertexBufferData(_vertexData, 0, 0, _quadCount * 4, 0, UpdateFlags);
        _mesh.SetSubMesh(0, new SubMeshDescriptor(0, _quadCount * 6) { bounds = MeshBounds, vertexCount = _quadCount * 4 }, UpdateFlags);
    }

    private static int CharacterCount(in Number number)
    {
        int count = 1;
        for (int value = number.Value; value >= 10 && count < MaxDigits - 1; value /= 10) count++;
        return number.Plus ? count + 1 : count;
    }

    // 사각형 quads개를 담을 자리. 삼각형 번호는 사각형마다 (0,1,2)(2,3,0)으로 늘 같아 늘릴 때 한 번만 만든다.
    private void EnsureCapacity(int quads)
    {
        if (quads <= _quadCapacity) return;
        _quadCapacity = Mathf.NextPowerOfTwo(Mathf.Max(quads, 64));
        if (_vertexData.IsCreated) _vertexData.Dispose();
        _vertexData = new NativeArray<Vertex>(_quadCapacity * 4, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        NativeArray<int> indices = new NativeArray<int>(_quadCapacity * 6, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        for (int q = 0; q < _quadCapacity; q++)
        {
            int v = q * 4;
            int t = q * 6;
            indices[t] = v;
            indices[t + 1] = v + 1;
            indices[t + 2] = v + 2;
            indices[t + 3] = v + 2;
            indices[t + 4] = v + 3;
            indices[t + 5] = v;
        }
        _mesh.SetVertexBufferParams(_quadCapacity * 4, Layout);
        _mesh.SetIndexBufferParams(_quadCapacity * 6, IndexFormat.UInt32);
        _mesh.SetIndexBufferData(indices, 0, 0, indices.Length, UpdateFlags);
        _mesh.subMeshCount = 1;
        _mesh.bounds = MeshBounds;
        indices.Dispose();
    }

    // 숫자 하나를 가운데 맞춰 글자마다 사각형으로 붙인다. right·up은 글자 단위 1이 맵 로컬로 얼마인지(늘었다 찌그러지기 포함).
    private void AppendNumber(in Number number, Vector3 center, Vector3 right, Vector3 up, float fontScale, float sdfScale, Color32 color, Vector3 normal)
    {
        // 자릿수를 뒤에서부터 뽑아 앞으로 돌린다(문자열을 만들지 않게). 자리 수는 CharacterCount와 같아야 한다.
        int count = 0;
        int value = number.Value;
        do
        {
            _digits[count++] = value % 10;
            value /= 10;
        }
        while (value > 0 && count < MaxDigits - 1);
        if (number.Plus) _digits[count++] = PlusIndex;

        // 가로는 글자 진행 폭의 가운데, 세로는 줄의 위아래 가운데(TMP 가운데 맞춤과 같다).
        float width = 0f;
        for (int d = count - 1; d >= 0; d--)
        {
            Glyph glyph = _glyphs[_digits[d]];
            if (glyph.Valid) width += glyph.Advance * glyph.Scale;
        }
        float pen = -width * 0.5f;
        float middle = (_ascent + _descent) * 0.5f;

        for (int d = count - 1; d >= 0; d--)
        {
            Glyph glyph = _glyphs[_digits[d]];
            if (!glyph.Valid) continue;
            float s = glyph.Scale;
            float left = (pen + glyph.Left * s) * fontScale;
            float rightEdge = (pen + glyph.Right * s) * fontScale;
            float bottom = (glyph.Bottom * s - middle) * fontScale;
            float top = (glyph.Top * s - middle) * fontScale;
            pen += glyph.Advance * s;

            Vector3 leftSide = center + right * left;
            Vector3 rightSide = center + right * rightEdge;
            Vector3 down = up * bottom;
            Vector3 high = up * top;
            float w = sdfScale * s;
            int at = _quadCount * 4;
            _vertexData[at] = new Vertex { Position = leftSide + down, Normal = normal, Color = color, Uv = new Vector4(glyph.UvMin.x, glyph.UvMin.y, 0f, w) };
            _vertexData[at + 1] = new Vertex { Position = leftSide + high, Normal = normal, Color = color, Uv = new Vector4(glyph.UvMin.x, glyph.UvMax.y, 0f, w) };
            _vertexData[at + 2] = new Vertex { Position = rightSide + high, Normal = normal, Color = color, Uv = new Vector4(glyph.UvMax.x, glyph.UvMax.y, 0f, w) };
            _vertexData[at + 3] = new Vertex { Position = rightSide + down, Normal = normal, Color = color, Uv = new Vector4(glyph.UvMax.x, glyph.UvMin.y, 0f, w) };
            _quadCount++;
        }
    }

    // TMP가 글자 사각형을 만드는 식 그대로(TMP_Text.SaveGlyphVertexInfo): 패딩만큼 사각형과 아틀라스 칸을 넓힌다.
    private void CacheGlyphs(TMP_FontAsset font, Material material)
    {
        _cachedFont = font;
        _cachedMaterial = material;
        _ascent = font.faceInfo.ascentLine;
        _descent = font.faceInfo.descentLine;
        // 동적 폰트면 아틀라스에 아직 없는 글자를 넣는다(정적이면 이미 있어야 하고, 부르면 경고만 남는다).
        if (font.atlasPopulationMode != AtlasPopulationMode.Static) font.TryAddCharacters(Characters);
        float padding = ShaderUtilities.GetPadding(material, false, false);
        float atlasWidth = font.atlasWidth;
        float atlasHeight = font.atlasHeight;

        for (int i = 0; i < Characters.Length; i++)
        {
            _glyphs[i] = default;
            if (!font.characterLookupTable.TryGetValue(Characters[i], out TMP_Character character) || character.glyph == null) continue;
            UnityEngine.TextCore.Glyph glyph = character.glyph;
            // 머티리얼은 첫 아틀라스만 본다 — 다른 장에 들어간 글자는 엉뚱한 칸을 그리므로 쓰지 않는다.
            if (glyph.atlasIndex != 0)
            {
                Debug.LogWarning($"[DamageNumbers] '{Characters[i]}' 글자가 {font.name}의 첫 아틀라스에 없어 그리지 않는다.");
                continue;
            }
            UnityEngine.TextCore.GlyphMetrics metrics = glyph.metrics;
            UnityEngine.TextCore.GlyphRect rect = glyph.glyphRect;
            _glyphs[i] = new Glyph
            {
                Valid = true,
                Left = metrics.horizontalBearingX - padding,
                Right = metrics.horizontalBearingX + metrics.width + padding,
                Bottom = metrics.horizontalBearingY - metrics.height - padding,
                Top = metrics.horizontalBearingY + padding,
                Advance = metrics.horizontalAdvance,
                Scale = character.scale * glyph.scale,
                UvMin = new Vector2((rect.x - padding) / atlasWidth, (rect.y - padding) / atlasHeight),
                UvMax = new Vector2((rect.x + rect.width + padding) / atlasWidth, (rect.y + rect.height + padding) / atlasHeight),
            };
        }
    }
}

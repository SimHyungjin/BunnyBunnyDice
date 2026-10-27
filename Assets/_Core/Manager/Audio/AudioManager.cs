using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

/// <summary>
/// 효과음(<see cref="SO_SoundCue"/>)과 배경음(<see cref="SO_MusicTrack"/>)을 재생한다.
/// 쓰는 쪽이 에셋을 직접 들고 오므로, 이 매니저는 아무것도 미리 로드하지 않는다.
///
/// 볼륨 값은 들고 적용만 한다. 저장·복원은 <see cref="SettingsManager"/>가 맡는다(부팅 때 저장값을 넣어 준다).
/// 볼륨은 AudioMixer(Resources/CoreAudioMixer)의 그룹 볼륨으로 다룬다. 재생 중인 소리에도 바로 반영되고,
/// 일시정지 먹먹함·덕킹 같은 연출은 믹서 스냅샷으로 붙인다. 믹서가 없으면 경고하고 소스 볼륨을 직접 곱하는 방식으로 대신한다.
///
/// 효과음 소스 수는 고정이다(<see cref="SO_CoreConfig.SfxPoolSize"/>). 모자라면 덜 중요하고 오래된 소리를 빼앗아 쓴다.
/// 배경음은 덱 두 개(각각 인트로·본체 소스)를 번갈아 쓰며 크로스페이드한다. 페이드는 매 프레임 직접 진행해,
/// 중단된 비동기 페이드가 뒤늦게 소스를 멈추는 경합이 생기지 않게 한다.
/// </summary>
public sealed class AudioManager : Manager
{
    private const string MixerResource = "CoreAudioMixer";
    private const string MasterParam = "MasterVolume";
    private const string MusicParam  = "MusicVolume";
    private const string SfxParam    = "SfxVolume";
    private const string MusicGroup  = "Music";
    private const string SfxGroup    = "SFX";

    // PlayScheduled는 dspTime이 이미 지난 시각을 받으면 이음새가 어긋난다. 그만큼 여유를 둔다.
    private const double ScheduleLead = 0.05;

    private static SO_CoreConfig Config => SO_CoreConfig.Instance;

    private GameObject _root;
    private AudioMixer _mixer;
    private AudioMixerGroup _musicGroup;
    private AudioMixerGroup _sfxGroup;

    // ── 채널 볼륨 ──
    private float _masterVolume = 1f;
    private float _musicVolume  = 1f;
    private float _soundVolume  = 1f;

    // 믹서 SetFloat은 믹서가 막 로드된 직후에는 무시될 수 있어, 다음 프레임 갱신 때 반영한다.
    private bool _volumesDirty = true;
    private bool _warnedMissingParam;

    #region 효과음 상태

    private sealed class Voice
    {
        public AudioSource Source;
        public SO_SoundCue Cue;            // 클립을 직접 낸 소리면 null
        public AudioChannelType Channel;
        public float BaseVolume;
        public int Priority;
        public float StartedAt;
        public int Generation = 1;         // 0은 기본값 핸들이 가리키지 않게 비워 둔다
        public bool Active;
    }

    private readonly List<Voice> _voices = new();
    private readonly Dictionary<SO_SoundCue, float> _lastPlayedAt = new();
    private readonly Dictionary<SO_SoundCue, int> _lastClipIndex = new();

    #endregion

    #region 배경음 상태

    private sealed class Deck
    {
        public AudioSource Intro;
        public AudioSource Body;
        public SO_MusicTrack Track;
        public float Gain;
        public float TargetGain;
        public float FadeSpeed;   // 초당 게인 변화량. 무한대면 즉시
        public double StartDsp;   // 예약 재생 시각. 이 전에는 아직 소리가 나지 않으니 "끝났다"고 판정하지 않는다
    }

    private Deck _deckA;
    private Deck _deckB;
    private Deck _current;

    /// <summary>지금 나오고 있는(또는 페이드 인 중인) 배경음. 없으면 null.</summary>
    public SO_MusicTrack CurrentMusic => _current?.Track;

    #endregion

    public override Type[] Dependencies => new[] { typeof(LoopManager) };

    #region Initialize

    protected override UniTask OnInitializeAsync()
    {
        _root = new GameObject("@Audio");
        Object.DontDestroyOnLoad(_root);

        _mixer = Resources.Load<AudioMixer>(MixerResource);
        if (_mixer != null)
        {
            _musicGroup = FindGroup(MusicGroup);
            _sfxGroup = FindGroup(SfxGroup);
        }
        else
        {
            Debug.LogWarning(
                $"[AudioManager] Resources에서 '{MixerResource}' 믹서를 찾지 못했습니다. " +
                "소스 볼륨을 직접 곱하는 방식으로 대신합니다(재생 중 볼륨 변경은 반영되지만 스냅샷·덕킹은 쓸 수 없습니다).");
        }

        _deckA = CreateDeck("Music_A");
        _deckB = CreateDeck("Music_B");

        int poolSize = Mathf.Max(1, Config.SfxPoolSize);
        for (int i = 0; i < poolSize; i++)
            _voices.Add(new Voice { Source = CreateSource($"SFX_{i}", _sfxGroup) });

        _volumesDirty = true;

        Core.Loop.OnUpdate += Tick;
        return UniTask.CompletedTask;
    }

    private AudioMixerGroup FindGroup(string name)
    {
        AudioMixerGroup[] groups = _mixer.FindMatchingGroups(name);
        for (int i = 0; i < groups.Length; i++)
            if (groups[i].name == name) return groups[i];

        Debug.LogWarning($"[AudioManager] 믹서 '{_mixer.name}'에 '{name}' 그룹이 없습니다. 그 소리는 Master로 나갑니다.");
        return null;
    }

    #endregion

    #region 효과음

    /// <summary>
    /// 큐를 재생한다. 위치를 넘기면 그 자리에서 3D로(큐의 SpatialBlend), 넘기지 않으면 2D로 난다.
    /// 간격 제한에 걸리거나 더 중요한 소리에 밀려 내지 못하면 기본값 핸들을 돌려준다.
    /// </summary>
    public SoundHandle PlaySfx(SO_SoundCue cue, Vector3? position = null)
    {
        if (cue == null || !cue.HasClips) return default;

        float now = Time.realtimeSinceStartup;
        if (cue.MinInterval > 0f && _lastPlayedAt.TryGetValue(cue, out float last) && now - last < cue.MinInterval)
            return default;

        if (cue.MaxInstances > 0 && CountActive(cue, out Voice oldestSame) >= cue.MaxInstances)
            Release(oldestSame);

        int index = AcquireVoice(cue.Priority);
        if (index < 0) return default;

        _lastPlayedAt[cue] = now;

        Voice voice = _voices[index];
        AudioSource src = voice.Source;
        src.clip = PickClip(cue);
        src.loop = cue.Loop;
        src.priority = cue.Priority;
        src.pitch = Random.Range(cue.PitchRange.x, cue.PitchRange.y);
        src.spatialBlend = position.HasValue ? cue.SpatialBlend : 0f;
        src.minDistance = cue.MinDistance;
        src.maxDistance = cue.MaxDistance;
        src.rolloffMode = cue.RolloffMode;

        return Start(index, cue, cue.Channel, cue.Volume, cue.Priority, position);
    }

    /// <summary>
    /// 큐 없이 클립을 그대로 낸다. 위치를 넘기면 3D로, 아니면 2D로. 설정은 기본값이다.
    /// 반복해서 쓰는 소리라면 큐로 만드는 편이 낫다(볼륨·피치·동시 재생 제한을 저작할 수 있다).
    /// </summary>
    public SoundHandle PlaySfx(AudioClip clip, Vector3? position = null, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return default;

        const int priority = 128;
        int index = AcquireVoice(priority);
        if (index < 0) return default;

        AudioSource src = _voices[index].Source;
        src.clip = clip;
        src.loop = false;
        src.priority = priority;
        src.pitch = pitch;
        src.spatialBlend = position.HasValue ? 1f : 0f;
        src.minDistance = 1f;
        src.maxDistance = 500f;
        src.rolloffMode = AudioRolloffMode.Logarithmic;

        return Start(index, null, AudioChannelType.Sound, Mathf.Clamp01(volume), priority, position);
    }

    public bool IsPlaying(SoundHandle handle) => Resolve(handle) != null;

    public void StopSfx(SoundHandle handle)
    {
        Voice voice = Resolve(handle);
        if (voice != null) Release(voice);
    }

    public void StopAllSfx()
    {
        for (int i = 0; i < _voices.Count; i++)
            if (_voices[i].Active) Release(_voices[i]);
    }

    private SoundHandle Start(int index, SO_SoundCue cue, AudioChannelType channel, float baseVolume, int priority, Vector3? position)
    {
        Voice voice = _voices[index];
        voice.Cue = cue;
        voice.Channel = channel;
        voice.BaseVolume = baseVolume;
        voice.Priority = priority;
        voice.StartedAt = Time.realtimeSinceStartup;
        voice.Active = true;

        AudioSource src = voice.Source;
        src.outputAudioMixerGroup = channel == AudioChannelType.Music ? _musicGroup : _sfxGroup;
        src.transform.position = position ?? Vector3.zero;
        src.volume = baseVolume * FallbackGain(channel);
        src.Play();

        return new SoundHandle(index, voice.Generation);
    }

    /// <summary>
    /// 쓸 소스를 고른다. 빈 것이 있으면 그것을, 없으면 이 소리보다 덜 중요한 것 중 가장 오래된 것을 빼앗는다.
    /// 모두 더 중요하면 -1(이 소리는 내지 않는다).
    /// </summary>
    private int AcquireVoice(int priority)
    {
        int victim = -1;
        for (int i = 0; i < _voices.Count; i++)
        {
            Voice v = _voices[i];
            if (!v.Active) return i;

            if (v.Priority < priority) continue;   // 숫자가 작을수록 중요하다
            if (victim < 0) { victim = i; continue; }

            Voice best = _voices[victim];
            if (v.Priority > best.Priority || (v.Priority == best.Priority && v.StartedAt < best.StartedAt))
                victim = i;
        }

        if (victim >= 0) Release(_voices[victim]);
        return victim;
    }

    private int CountActive(SO_SoundCue cue, out Voice oldest)
    {
        int count = 0;
        oldest = null;
        for (int i = 0; i < _voices.Count; i++)
        {
            Voice v = _voices[i];
            if (!v.Active || v.Cue != cue) continue;

            count++;
            if (oldest == null || v.StartedAt < oldest.StartedAt) oldest = v;
        }
        return count;
    }

    private AudioClip PickClip(SO_SoundCue cue)
    {
        AudioClip[] clips = cue.Clips;
        int valid = 0;
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null) valid++;

        if (!_lastClipIndex.TryGetValue(cue, out int last)) last = -1;

        // 둘 이상이면 직전 것을 빼고 고른다. 같은 소리가 연달아 나면 무작위로 느껴지지 않는다.
        bool excludeLast = valid > 1 && last >= 0 && last < clips.Length && clips[last] != null;
        int pick = Random.Range(0, excludeLast ? valid - 1 : valid);

        int index = -1;
        for (int i = 0, n = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;
            if (excludeLast && i == last) continue;
            if (n++ == pick) { index = i; break; }
        }

        _lastClipIndex[cue] = index;
        return clips[index];
    }

    private Voice Resolve(SoundHandle handle)
    {
        if (handle.Voice < 0 || handle.Voice >= _voices.Count) return null;

        Voice voice = _voices[handle.Voice];
        return voice.Active && voice.Generation == handle.Generation ? voice : null;
    }

    private void Release(Voice voice)
    {
        voice.Active = false;
        voice.Cue = null;
        voice.Generation++;
        voice.Source.Stop();
        voice.Source.clip = null;
    }

    #endregion

    #region 배경음

    /// <summary>
    /// 배경음을 바꾼다. 같은 곡이면 아무것도 하지 않는다. null이면 멈춘다.
    /// </summary>
    /// <param name="crossfade">음수면 설정(SO_CoreConfig)의 기본 크로스페이드 시간을 쓴다.</param>
    public void PlayMusic(SO_MusicTrack track, float crossfade = -1f)
    {
        // 기본 인자에는 컴파일 타임 상수만 올 수 있어 설정값을 직접 넣을 수 없다. 음수를 "설정을 따르라"는 표시로 쓴다.
        if (crossfade < 0f) crossfade = Config.BgmCrossfadeDuration;

        if (track == null)
        {
            StopMusic(crossfade);
            return;
        }

        if (_current != null && _current.Track == track) return;

        if (!track.HasClip)
        {
            Debug.LogWarning($"[AudioManager] 배경음 '{track.name}'에 클립이 없습니다.");
            return;
        }

        // 쓰지 않는 덱을 고른다. 그 덱이 아직 페이드 아웃 중이었다면(빠르게 곡을 연달아 바꾼 경우) 여기서 끊는다.
        Deck next = _current == _deckA ? _deckB : _deckA;
        StopDeck(next);

        if (_current != null) FadeDeck(_current, 0f, crossfade);

        StartDeck(next, track);
        next.Gain = crossfade > 0f ? 0f : 1f;
        FadeDeck(next, 1f, crossfade);
        ApplyDeckVolume(next);

        _current = next;
    }

    /// <param name="fade">음수면 설정(SO_CoreConfig)의 기본 페이드 아웃 시간을 쓴다.</param>
    public void StopMusic(float fade = -1f)
    {
        if (fade < 0f) fade = Config.BgmStopFadeDuration;
        if (_current == null) return;

        FadeDeck(_current, 0f, fade);
        _current = null;
    }

    private void StartDeck(Deck deck, SO_MusicTrack track)
    {
        deck.Track = track;
        double start = AudioSettings.dspTime + ScheduleLead;
        deck.StartDsp = start;

        if (track.Intro != null)
        {
            deck.Intro.clip = track.Intro;
            deck.Intro.loop = false;
            deck.Intro.PlayScheduled(start);

            // 초가 아니라 샘플 수로 길이를 잰다. 그래야 본체가 인트로 끝 샘플에 정확히 붙는다.
            start += (double)track.Intro.samples / track.Intro.frequency;
        }

        if (track.Body != null)
        {
            deck.Body.clip = track.Body;
            deck.Body.loop = track.Loop;
            deck.Body.PlayScheduled(start);
        }
    }

    private void StopDeck(Deck deck)
    {
        deck.Intro.Stop();
        deck.Body.Stop();
        deck.Intro.clip = null;
        deck.Body.clip = null;
        deck.Track = null;
        deck.Gain = 0f;
        deck.TargetGain = 0f;
    }

    private static void FadeDeck(Deck deck, float target, float duration)
    {
        deck.TargetGain = target;
        deck.FadeSpeed = duration > 0f ? 1f / duration : float.PositiveInfinity;
    }

    private void TickDeck(Deck deck, float dt)
    {
        if (deck.Track == null) return;

        if (!Mathf.Approximately(deck.Gain, deck.TargetGain))
        {
            deck.Gain = float.IsPositiveInfinity(deck.FadeSpeed)
                ? deck.TargetGain
                : Mathf.MoveTowards(deck.Gain, deck.TargetGain, deck.FadeSpeed * dt);
            ApplyDeckVolume(deck);
        }

        if (deck.TargetGain <= 0f && deck.Gain <= 0f)
        {
            StopDeck(deck);
            return;
        }

        // 반복하지 않는 곡이 끝났으면 덱을 비운다.
        if (!deck.Track.Loop && AudioSettings.dspTime > deck.StartDsp
            && !deck.Intro.isPlaying && !deck.Body.isPlaying && !AudioListener.pause)
        {
            if (_current == deck) _current = null;
            StopDeck(deck);
        }
    }

    private void ApplyDeckVolume(Deck deck)
    {
        if (deck.Track == null) return;

        float volume = deck.Track.Volume * deck.Gain * FallbackGain(AudioChannelType.Music);
        deck.Intro.volume = volume;
        deck.Body.volume = volume;
    }

    #endregion

    #region 채널 볼륨

    /// <summary>채널 볼륨을 적용한다. 저장하지 않는다 — 유저 설정으로 바꾸려면 <see cref="SettingsManager.SetVolume"/>을 쓴다.</summary>
    public void SetVolume(AudioChannelType channel, float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(GetVolume(channel), value)) return;

        switch (channel)
        {
            case AudioChannelType.Master: _masterVolume = value; break;
            case AudioChannelType.Music:  _musicVolume  = value; break;
            case AudioChannelType.Sound:  _soundVolume  = value; break;
        }

        _volumesDirty = true;
        ApplyVolumes();
    }

    public float GetVolume(AudioChannelType channel) => channel switch
    {
        AudioChannelType.Master => _masterVolume,
        AudioChannelType.Music  => _musicVolume,
        AudioChannelType.Sound  => _soundVolume,
        _                       => 1f,
    };

    private void ApplyVolumes()
    {
        _volumesDirty = false;

        if (_mixer != null)
        {
            bool ok = _mixer.SetFloat(MasterParam, ToDecibel(_masterVolume))
                    & _mixer.SetFloat(MusicParam, ToDecibel(_musicVolume))
                    & _mixer.SetFloat(SfxParam, ToDecibel(_soundVolume));

            if (!ok && !_warnedMissingParam)
            {
                _warnedMissingParam = true;
                Debug.LogWarning(
                    $"[AudioManager] 믹서에 노출된 파라미터 '{MasterParam}'·'{MusicParam}'·'{SfxParam}' 중 없는 것이 있습니다. " +
                    "그룹의 Volume을 우클릭해 Expose하고 이 이름으로 바꾸세요.");
            }
            return;
        }

        // 믹서가 없을 때: 지금 나고 있는 소리에 직접 반영한다.
        for (int i = 0; i < _voices.Count; i++)
        {
            Voice v = _voices[i];
            if (v.Active) v.Source.volume = v.BaseVolume * FallbackGain(v.Channel);
        }
        ApplyDeckVolume(_deckA);
        ApplyDeckVolume(_deckB);
    }

    // 믹서가 볼륨을 맡으면 소스 쪽에서는 곱하지 않는다.
    private float FallbackGain(AudioChannelType channel)
    {
        if (_mixer != null) return 1f;
        return _masterVolume * (channel == AudioChannelType.Music ? _musicVolume : _soundVolume);
    }

    // 믹서 볼륨은 dB다. 0~1을 그대로 넣으면 대부분의 구간이 거의 들리지 않거나 너무 크다.
    private static float ToDecibel(float linear) => Mathf.Log10(Mathf.Max(linear, 0.0001f)) * 20f;

    #endregion

    #region Tick · 정리

    private void Tick(float dt)
    {
        if (_volumesDirty) ApplyVolumes();

        // 끝난 효과음 소스를 돌려받는다. 길이를 계산해 기다리지 않고 실제로 멈췄는지를 본다
        // — 피치·일시정지와 무관하게 정확하고, 기다리는 비동기 작업도 남지 않는다.
        if (!AudioListener.pause)
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                Voice v = _voices[i];
                if (v.Active && !v.Source.isPlaying) Release(v);
            }
        }

        TickDeck(_deckA, dt);
        TickDeck(_deckB, dt);
    }

    /// <summary>
    /// 씬 전환 정리. 효과음은 모두 멈춘다(이전 씬 자리에서 나던 소리다).
    /// 배경음은 그대로 둔다 — 씬이 바뀌어도 같은 곡이면 이어져야 하고, 바꿀지는 새 씬이 PlayMusic으로 정한다.
    /// </summary>
    public override void Clear() => StopAllSfx();

    private Deck CreateDeck(string name)
        => new()
        {
            Intro = CreateSource($"{name}_Intro", _musicGroup),
            Body  = CreateSource($"{name}_Body", _musicGroup),
        };

    private AudioSource CreateSource(string name, AudioMixerGroup group)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root.transform);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.outputAudioMixerGroup = group;
        return src;
    }

    #endregion
}

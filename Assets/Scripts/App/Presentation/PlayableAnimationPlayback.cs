using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// 基于 PlayableGraph 的播放后端：层 0 保留中断权重的多片混合，层 1 Additive。
/// Manual 时间由 Simulation Tick 推进，保证 RootMotion delta 与逻辑步对齐。
/// </summary>
public sealed class PlayableAnimationPlayback : IAnimationPlayback
{
    const int InputCount = 2;
    const int BaseLayer = 0;
    const int AdditiveLayer = 1;
    const float DefaultAdditiveFadeOut = 0.05f;
    static readonly ProfilerMarker PlayMarker = new("ACTGame.Animation.SwitchClip");
    static readonly ProfilerMarker SeekMarker = new("ACTGame.Animation.Seek");
    static readonly ProfilerMarker TickMarker = new("ACTGame.Animation.Tick");

    readonly Animator _animator;
    PlayableGraph _graph;
    AnimationLayerMixerPlayable _layerMixer;
    AnimationMixerPlayable _mixer;
    // 每次换片记录实际权重；中途换向不把尚未淡入完成的目标提升到满权。
    sealed class ClipSlot
    {
        public AnimationClip Clip;
        public AnimationClipPlayable Playable;
        public object TimeGroup;
        public float Weight;
        public float FadeStartWeight;
    }
    readonly List<ClipSlot> _slots = new();
    ClipSlot _currentSlot;
    AnimationClipPlayable _currentPlayable;
    AnimationClipPlayable _additivePlayable;
    AnimationClip _currentClip;
    AnimationClip _additiveClip;
    float _fadeDuration;
    float _fadeElapsed;
    bool _fading;
    float _additiveWeight;
    float _additiveFadeIn;
    float _additiveElapsed;
    float _additiveHoldSeconds;
    float _speed = 1f;
    bool _disposed;

    /// <summary>创建播放后端；会清空 runtimeAnimatorController，改由 Playable 驱动。</summary>
    public PlayableAnimationPlayback(Animator animator)
    {
        _animator = animator;
        if (_animator == null)
            return;

        // 运行时脱钩 Controller，避免与 Playable 双轨抢控制权。
        _animator.runtimeAnimatorController = null;

        _graph = PlayableGraph.Create($"{animator.name}_CharacterAnimation");
        // Manual：禁止 GameTime 与逻辑步双轨推进，否则逐帧 Seek 会污染 Animator.deltaPosition。
        _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        _mixer = AnimationMixerPlayable.Create(_graph, 0);
        _layerMixer = AnimationLayerMixerPlayable.Create(_graph, InputCount);
        _layerMixer.ConnectInput(BaseLayer, _mixer, 0);
        _layerMixer.SetInputWeight(BaseLayer, 1f);
        _layerMixer.SetLayerAdditive(AdditiveLayer, true);
        _layerMixer.SetInputWeight(AdditiveLayer, 0f);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);
        output.SetSourcePlayable(_layerMixer);
        _graph.Play();
    }

    public bool IsValid => !_disposed && _animator != null && _graph.IsValid();

    public float Speed
    {
        get => _speed;
        set
        {
            _speed = Mathf.Max(0f, value);
            ApplySpeed();
        }
    }

    public AnimationClip CurrentClip => _currentClip;

    /// <inheritdoc />
    public float AdditiveWeight => _additiveWeight;

    public float NormalizedTime
    {
        get
        {
            if (_currentClip == null || _currentClip.length <= 0f || !_currentPlayable.IsValid())
                return 0f;

            return (float)(_currentPlayable.GetTime() / _currentClip.length);
        }
    }

    public bool HasFinished
    {
        get
        {
            if (!IsValid || _currentClip == null || !_currentPlayable.IsValid())
                return true;

            if (_fading || _currentClip.isLooping)
                return false;

            return _currentPlayable.GetTime() >= _currentClip.length;
        }
    }

    /// <summary>从现有混合权重淡入。相同非空时钟组内复用片段，避免频繁换向重复建图。</summary>
    public void Play(AnimationClip clip, float fadeDuration, object timeGroup = null)
    {
        using var marker = PlayMarker.Auto();
        if (!IsValid || clip == null)
            return;

        // 切主 Clip（走跑键或出招段）时清 Additive，避免探针残留到下一招。
        StopAdditive();
        for (int i = _slots.Count - 1; i >= 0; i--)
            if (_slots[i].Weight <= 0f) RemoveSlot(i);
        _currentSlot = timeGroup == null ? null : _slots.Find(
            slot => slot.Clip == clip && ReferenceEquals(slot.TimeGroup, timeGroup));
        if (_currentSlot == null)
        {
            var playable = AnimationClipPlayable.Create(_graph, clip);
            playable.SetApplyFootIK(true);
            playable.SetTime(0.0);
            playable.SetTime(0.0);
            playable.Play();
            _currentSlot = new ClipSlot { Clip = clip, Playable = playable, TimeGroup = timeGroup };
            _slots.Add(_currentSlot);
            _mixer.SetInputCount(_slots.Count);
            _mixer.ConnectInput(_slots.Count - 1, playable, 0);
        }
        _currentPlayable = _currentSlot.Playable;
        foreach (ClipSlot slot in _slots) slot.FadeStartWeight = slot.Weight;

        _currentClip = clip;
        _fadeDuration = Mathf.Max(0f, fadeDuration);
        _fadeElapsed = 0f;

        if (_fadeDuration <= 0f || _slots.Count == 1)
        {
            ApplyBlend(1f);
            _fading = false;
            return;
        }

        ApplyBlend(0f);
        _fading = true;
    }

    /// <summary>将当前主 Clip 跳到指定时间并立即采样姿态；保留 CrossFade，不推进时间以免产生虚假 RootMotion。</summary>
    public void Seek(float timeSeconds)
    {
        using var marker = SeekMarker.Auto();
        if (!IsValid || !_currentPlayable.IsValid())
            return;

        SetSampleTime(timeSeconds);
        _graph.Evaluate(0f);
    }

    bool SharesCurrentClock(ClipSlot slot) => slot == _currentSlot
        || (_currentSlot.TimeGroup != null && ReferenceEquals(slot.TimeGroup, _currentSlot.TimeGroup));

    void SetSampleTime(float timeSeconds)
    {
        foreach (ClipSlot slot in _slots)
        {
            if (!SharesCurrentClock(slot)) continue;
            double clamped = Mathf.Clamp(timeSeconds, 0f, slot.Clip.length);
            // 同一 Action 段内的 Pose/Move 同步定位，包括仍在淡出的片段。
            slot.Playable.SetTime(clamped);
            slot.Playable.SetTime(clamped);
        }
    }

    /// <inheritdoc />
    public void Sample(float timeSeconds, float deltaTime)
    {
        using var marker = SeekMarker.Auto();
        if (!IsValid || !_currentPlayable.IsValid()) return;
        float dt = Mathf.Max(0f, deltaTime);
        AdvanceBlend(dt);
        SetSampleTime(timeSeconds);
        // 当前时钟组由调用方指定绝对时间，图求值时不可再前进 dt。
        // 其它淡出片与 Additive 仍随图正常推进，不把上一段拖回新段起点。
        foreach (ClipSlot slot in _slots)
            if (SharesCurrentClock(slot)) slot.Playable.SetSpeed(0);
        try { _graph.Evaluate(dt); }
        finally
        {
            foreach (ClipSlot slot in _slots)
                if (SharesCurrentClock(slot)) slot.Playable.SetSpeed(1);
        }
    }

    /// <summary>推进 CrossFade 权重，并以固定步长 Evaluate Graph（唯一时间推进入口）。</summary>
    public void Tick(float deltaTime)
    {
        using var marker = TickMarker.Auto();
        if (!IsValid)
            return;

        float dt = Mathf.Max(0f, deltaTime);
        AdvanceBlend(dt);
        _graph.Evaluate(dt);
    }

    void AdvanceBlend(float dt)
    {
        if (_fading)
        {
            // CrossFade：按速度推进权重，旧片→新片
            _fadeElapsed += dt * _speed;
            float t = _fadeDuration <= 0f ? 1f : Mathf.Clamp01(_fadeElapsed / _fadeDuration);
            ApplyBlend(t);

            if (t >= 1f)
            {
                // 淡入结束：只留当前槽，销毁上一 Clip
                _fading = false;
            }
        }

        TickAdditive(dt);

    }

    /// <inheritdoc />
    public void PlayAdditive(AnimationClip clip, AvatarMask mask, float fadeDuration)
    {
        if (!IsValid || clip == null)
            return;

        DisconnectAdditive();

        _additivePlayable = AnimationClipPlayable.Create(_graph, clip);
        _additivePlayable.SetApplyFootIK(false);
        _additivePlayable.SetTime(0.0);
        _additivePlayable.SetTime(0.0);
        _additivePlayable.Play();
        _layerMixer.ConnectInput(AdditiveLayer, _additivePlayable, 0);
        if (mask != null)
            _layerMixer.SetLayerMaskFromAvatarMask((uint)AdditiveLayer, mask);

        _additiveClip = clip;
        _additiveFadeIn = Mathf.Max(0f, fadeDuration);
        _additiveElapsed = 0f;
        _additiveHoldSeconds = Mathf.Max(0.0001f, clip.length);
        _additiveWeight = _additiveFadeIn <= 0f ? 1f : 0f;
        _layerMixer.SetInputWeight(AdditiveLayer, _additiveWeight);
    }

    /// <inheritdoc />
    public void StopAdditive()
    {
        DisconnectAdditive();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _currentClip = null;
        _additiveClip = null;
        _fading = false;
        _additiveWeight = 0f;

        if (_graph.IsValid())
            _graph.Destroy();

        _slots.Clear();
        _currentSlot = null;
        _currentPlayable = default;
        _additivePlayable = default;
        _mixer = default;
        _layerMixer = default;
    }

    void ApplyBlend(float progress)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            ClipSlot slot = _slots[i];
            slot.Weight = Mathf.Lerp(slot.FadeStartWeight, slot == _currentSlot ? 1f : 0f, progress);
            _mixer.SetInputWeight(i, slot.Weight);
        }
        if (progress >= 1f)
            for (int i = _slots.Count - 1; i >= 0; i--)
                if (_slots[i] != _currentSlot) RemoveSlot(i);
    }

    void RemoveSlot(int index)
    {
        ClipSlot removed = _slots[index];
        for (int i = index; i < _slots.Count; i++) _mixer.DisconnectInput(i);
        removed.Playable.Destroy();
        _slots.RemoveAt(index);
        _mixer.SetInputCount(_slots.Count);
        for (int i = index; i < _slots.Count; i++)
        {
            _mixer.ConnectInput(i, _slots[i].Playable, 0);
            _mixer.SetInputWeight(i, _slots[i].Weight);
        }
    }

    void ApplySpeed()
    {
        if (_mixer.IsValid())
            _mixer.SetSpeed(_speed);
        if (_layerMixer.IsValid())
            _layerMixer.SetSpeed(_speed);
    }

    /// <summary>按 Clip 时长推进 Additive 淡入/淡出；播完后自动清层，避免残留权重。</summary>
    void TickAdditive(float dt)
    {
        if (!_additivePlayable.IsValid() || !_layerMixer.IsValid())
            return;

        _additiveElapsed += dt * _speed;
        float fade = _additiveFadeIn > 0f ? _additiveFadeIn : DefaultAdditiveFadeOut;
        float weight;
        if (_additiveElapsed < _additiveFadeIn)
            weight = _additiveFadeIn <= 0f ? 1f : Mathf.Clamp01(_additiveElapsed / _additiveFadeIn);
        else if (_additiveElapsed >= _additiveHoldSeconds)
        {
            float over = _additiveElapsed - _additiveHoldSeconds;
            if (over >= fade)
            {
                DisconnectAdditive();
                return;
            }

            weight = 1f - Mathf.Clamp01(over / fade);
        }
        else
            weight = 1f;

        _additiveWeight = weight;
        _layerMixer.SetInputWeight(AdditiveLayer, weight);
    }

    /// <summary>断开 Additive Clip 并将层权置 0；主槽不受影响。</summary>
    void DisconnectAdditive()
    {
        _additiveWeight = 0f;
        _additiveClip = null;
        _additiveElapsed = 0f;
        _additiveHoldSeconds = 0f;
        _additiveFadeIn = 0f;

        if (_layerMixer.IsValid())
        {
            if (_layerMixer.GetInputCount() > AdditiveLayer
                && _layerMixer.GetInput(AdditiveLayer).IsValid())
            {
                _layerMixer.DisconnectInput(AdditiveLayer);
            }

            _layerMixer.SetInputWeight(AdditiveLayer, 0f);
        }

        if (_additivePlayable.IsValid())
            _additivePlayable.Destroy();

        _additivePlayable = default;
    }
}

using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// 基于 PlayableGraph 的播放后端：层 0 双槽 Override CrossFade，层 1 Additive。
/// Manual 时间由 Simulation Tick 推进，保证 RootMotion delta 与逻辑步对齐。
/// </summary>
public sealed class PlayableAnimationPlayback : IAnimationPlayback
{
    const int InputCount = 2;
    const int PreviousSlot = 0;
    const int CurrentSlot = 1;
    const int BaseLayer = 0;
    const int AdditiveLayer = 1;
    const float DefaultAdditiveFadeOut = 0.05f;

    readonly Animator _animator;
    PlayableGraph _graph;
    AnimationLayerMixerPlayable _layerMixer;
    AnimationMixerPlayable _mixer;
    AnimationClipPlayable _previousPlayable;
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
        _mixer = AnimationMixerPlayable.Create(_graph, InputCount);
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

    /// <summary>淡入播放；将当前槽挪到上一槽后接入新 Clip，fade≤0 或无上一层时立即切满权。</summary>
    public void Play(AnimationClip clip, float fadeDuration)
    {
        if (!IsValid || clip == null)
            return;

        // 切主 Clip（走跑键或出招段）时清 Additive，避免探针残留到下一招。
        StopAdditive();
        PromoteCurrentToPrevious();

        _currentPlayable = AnimationClipPlayable.Create(_graph, clip);
        _currentPlayable.SetApplyFootIK(true);
        _currentPlayable.SetTime(0.0);
        _currentPlayable.SetTime(0.0);
        _currentPlayable.Play();
        _mixer.ConnectInput(CurrentSlot, _currentPlayable, 0);

        _currentClip = clip;
        _fadeDuration = Mathf.Max(0f, fadeDuration);
        _fadeElapsed = 0f;

        if (_fadeDuration <= 0f || !_previousPlayable.IsValid())
        {
            SetWeights(0f, 1f);
            DestroySlot(PreviousSlot, ref _previousPlayable);
            _fading = false;
            return;
        }

        SetWeights(1f, 0f);
        _fading = true;
    }

    /// <summary>将当前主 Clip 跳到指定时间并立即采样姿态；保留 CrossFade，不推进时间以免产生虚假 RootMotion。</summary>
    public void Seek(float timeSeconds)
    {
        if (!IsValid || !_currentPlayable.IsValid())
            return;

        double clamped = Mathf.Max(0f, timeSeconds);
        if (_currentClip != null)
            clamped = Mathf.Min((float)clamped, _currentClip.length);

        // Unity AnimationClipPlayable 偶发需连续 SetTime 两次才生效。
        _currentPlayable.SetTime(clamped);
        _currentPlayable.SetTime(clamped);
        // Evaluate(0) 只应用姿态；调用方若开启 RootMotion，应在 Seek 期间临时关闭以免跳变位移。
        _graph.Evaluate(0f);
    }

    /// <summary>推进 CrossFade 权重，并以固定步长 Evaluate Graph（唯一时间推进入口）。</summary>
    public void Tick(float deltaTime)
    {
        if (!IsValid)
            return;

        float dt = Mathf.Max(0f, deltaTime);
        if (_fading)
        {
            // CrossFade：按速度推进权重，旧片→新片
            _fadeElapsed += dt * _speed;
            float t = _fadeDuration <= 0f ? 1f : Mathf.Clamp01(_fadeElapsed / _fadeDuration);
            SetWeights(1f - t, t);

            if (t >= 1f)
            {
                // 淡入结束：只留当前槽，销毁上一 Clip
                SetWeights(0f, 1f);
                DestroySlot(PreviousSlot, ref _previousPlayable);
                _fading = false;
            }
        }

        TickAdditive(dt);

        // 唯一时间推进入口：固定步长 Evaluate Graph
        _graph.Evaluate(dt);
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

        _previousPlayable = default;
        _currentPlayable = default;
        _additivePlayable = default;
        _mixer = default;
        _layerMixer = default;
    }

    void PromoteCurrentToPrevious()
    {
        DestroySlot(PreviousSlot, ref _previousPlayable);

        if (!_currentPlayable.IsValid())
            return;

        _mixer.DisconnectInput(CurrentSlot);
        _previousPlayable = _currentPlayable;
        _currentPlayable = default;
        _mixer.ConnectInput(PreviousSlot, _previousPlayable, 0);
    }

    void DestroySlot(int slot, ref AnimationClipPlayable playable)
    {
        if (_mixer.IsValid() && _mixer.GetInputCount() > slot && _mixer.GetInput(slot).IsValid())
            _mixer.DisconnectInput(slot);

        if (playable.IsValid())
            playable.Destroy();

        playable = default;
        if (_mixer.IsValid())
            _mixer.SetInputWeight(slot, 0f);
    }

    void SetWeights(float previousWeight, float currentWeight)
    {
        if (!_mixer.IsValid())
            return;

        _mixer.SetInputWeight(PreviousSlot, previousWeight);
        _mixer.SetInputWeight(CurrentSlot, currentWeight);
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

using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>基于 PlayableGraph 的双槽 CrossFade 播放后端；Manual 时间由 Simulation Tick 推进，保证 RootMotion delta 与逻辑步对齐。</summary>
public sealed class PlayableAnimationPlayback : IAnimationPlayback
{
    const int InputCount = 2;
    const int PreviousSlot = 0;
    const int CurrentSlot = 1;

    readonly Animator _animator;
    PlayableGraph _graph;
    AnimationMixerPlayable _mixer;
    AnimationClipPlayable _previousPlayable;
    AnimationClipPlayable _currentPlayable;
    AnimationClip _currentClip;
    float _fadeDuration;
    float _fadeElapsed;
    bool _fading;
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
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);
        output.SetSourcePlayable(_mixer);
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
            _fadeElapsed += dt * _speed;
            float t = _fadeDuration <= 0f ? 1f : Mathf.Clamp01(_fadeElapsed / _fadeDuration);
            SetWeights(1f - t, t);

            if (t >= 1f)
            {
                SetWeights(0f, 1f);
                DestroySlot(PreviousSlot, ref _previousPlayable);
                _fading = false;
            }
        }

        _graph.Evaluate(dt);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _currentClip = null;
        _fading = false;

        if (_graph.IsValid())
            _graph.Destroy();

        _previousPlayable = default;
        _currentPlayable = default;
        _mixer = default;
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
        if (!_mixer.IsValid())
            return;

        _mixer.SetSpeed(_speed);
    }
}

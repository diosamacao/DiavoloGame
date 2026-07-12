using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>基于 PlayableGraph 的双槽 CrossFade 播放后端；Animator 仅作输出目标，不依赖 Controller。</summary>
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
        _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
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

    /// <summary>将当前主 Clip 跳到指定时间；同时清淡入以免权重卡在过渡中。</summary>
    public void Seek(float timeSeconds)
    {
        if (!IsValid || !_currentPlayable.IsValid())
            return;

        double clamped = Mathf.Max(0f, timeSeconds);
        if (_currentClip != null)
            clamped = Mathf.Min((float)clamped, _currentClip.length);

        _currentPlayable.SetTime(clamped);
        _currentPlayable.SetTime(clamped);
        SetWeights(0f, 1f);
        DestroySlot(PreviousSlot, ref _previousPlayable);
        _fading = false;
    }

    /// <summary>按 Speed 推进 CrossFade 权重；Speed=0 时淡入与骨骼一并冻结。</summary>
    public void Tick(float deltaTime)
    {
        if (!IsValid || !_fading)
            return;

        _fadeElapsed += Mathf.Max(0f, deltaTime) * _speed;
        float t = _fadeDuration <= 0f ? 1f : Mathf.Clamp01(_fadeElapsed / _fadeDuration);
        SetWeights(1f - t, t);

        if (t < 1f)
            return;

        SetWeights(0f, 1f);
        DestroySlot(PreviousSlot, ref _previousPlayable);
        _fading = false;
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

using UnityEngine;

/// <summary>无 Graph 的动画后端；Headless Authority 使用，不推进骨骼或 Clip。</summary>
public sealed class NullAnimationPlayback : IAnimationPlayback
{
    /// <inheritdoc />
    public bool IsValid => false;

    /// <inheritdoc />
    public float Speed { get; set; } = 1f;

    /// <inheritdoc />
    public AnimationClip CurrentClip => null;

    /// <inheritdoc />
    public float NormalizedTime => 0f;

    /// <inheritdoc />
    public bool HasFinished => false;

    /// <inheritdoc />
    public void Play(AnimationClip clip, float fadeDuration)
    {
    }

    /// <inheritdoc />
    public void Seek(float timeSeconds)
    {
    }

    /// <inheritdoc />
    public void Tick(float deltaTime)
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

/// <summary>Headless 脚步表现空实现。</summary>
public sealed class NullLocomotionFootstepSink : ILocomotionFootstepSink
{
    /// <summary>共享无状态实例。</summary>
    public static readonly NullLocomotionFootstepSink Instance = new();

    NullLocomotionFootstepSink()
    {
    }

    /// <inheritdoc />
    public void PlayIfPlanted(FootSide? planted)
    {
    }
}

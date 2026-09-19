/// <summary>Headless 动作表现空实现；保留统一调用路径且不创建任何 Unity 表现对象。</summary>
public sealed class NullActionPresentationSink : IActionPresentationSink
{
    /// <summary>共享无状态实例。</summary>
    public static readonly NullActionPresentationSink Instance = new();

    NullActionPresentationSink()
    {
    }

    /// <inheritdoc />
    public void ConsumeEvent(in ActionSimEvent actionEvent)
    {
    }

    /// <inheritdoc />
    public void ApplyBeforeGameplay(in ActionSimSnapshot snapshot, float fixedDeltaSeconds)
    {
    }

    /// <inheritdoc />
    public void ApplyAfterGameplay(in ActionSimSnapshot snapshot)
    {
    }

    /// <inheritdoc />
    public void CompleteSimulationStep(in ActionSimSnapshot snapshot, float fixedDeltaSeconds)
    {
    }

    /// <inheritdoc />
    public void ResetForVisibilityLoss()
    {
    }

    /// <inheritdoc />
    public void ConsumeFlinch(AnimationKey key, in ActionHitContext context)
    {
    }
}

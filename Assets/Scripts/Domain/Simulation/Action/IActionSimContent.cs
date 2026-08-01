/// <summary>向纯动作模拟核提供已迁移为 60Hz 的只读动作帧内容。</summary>
public interface IActionSimContent
{
    /// <summary>内容是否已完成模拟数据迁移并可作为权威逻辑输入。</summary>
    bool IsSimulationReady { get; }

    /// <summary>内容采样率；ActionSim 仅接受 60Hz。</summary>
    int SampleRate { get; }

    /// <summary>动作终止哨兵帧；有效动作帧范围为 0 到 TotalFrames-1。</summary>
    int TotalFrames { get; }

    /// <summary>动作硬打断优先级。</summary>
    int InterruptPriority { get; }

    /// <summary>返回指定动作帧是否允许被更高优先级动作硬打断。</summary>
    bool IsInterruptibleAtFrame(int frame);

    /// <summary>返回指定类型的取消窗口在动作帧是否开放。</summary>
    bool IsCancelWindowActiveAtFrame(CancelWindowType windowType, int frame);

    /// <summary>返回 Recovery 在动作帧是否允许从图入口软重开。</summary>
    bool AllowsRecoveryEntryRestartAtFrame(int frame);

    /// <summary>返回 Recovery 在动作帧是否允许移动取消。</summary>
    bool AllowsMovementCancelAtFrame(int frame);
}

/// <summary>运行时解析后的取消窗口。</summary>
public readonly struct ResolvedCancelWindow
{
    public ResolvedCancelWindow(
        int startFrame,
        int endFrame,
        CancelType cancelType,
        string cancelSlotId,
        int priority)
    {
        StartFrame = startFrame;
        EndFrame = endFrame;
        CancelType = cancelType;
        CancelSlotId = cancelSlotId ?? string.Empty;
        Priority = priority;
    }

    /// <summary>窗口开始逻辑帧。</summary>
    public int StartFrame { get; }

    /// <summary>窗口结束逻辑帧。</summary>
    public int EndFrame { get; }

    /// <summary>取消类型：Action 连招进位、Recovery 后摇重开，或 Movement 移动取消。</summary>
    public CancelType CancelType { get; }

    /// <summary>Cancel 槽 id（= CancelWindow 时间轴条目 Id），供图边匹配。</summary>
    public string CancelSlotId { get; }

    /// <summary>同帧多个窗口命中时的优先级。</summary>
    public int Priority { get; }

    /// <summary>指定帧是否落在取消窗口内；窗口至少跨 1 帧才生效。</summary>
    public bool IsActiveAtFrame(int frame) =>
        EndFrame > StartFrame && frame >= StartFrame && frame <= EndFrame;
}

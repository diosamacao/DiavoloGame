using System;

/// <summary>运行时解析后的取消窗口，避免执行阶段直接触碰 InputActionReference。</summary>
public readonly struct ResolvedCancelWindow
{
    public ResolvedCancelWindow(
        int startFrame,
        int endFrame,
        CancelType cancelType,
        string[] allowedInputIds,
        int priority)
    {
        StartFrame = startFrame;
        EndFrame = endFrame;
        CancelType = cancelType;
        AllowedInputs = allowedInputIds ?? Array.Empty<string>();
        Priority = priority;
    }

    /// <summary>窗口开始逻辑帧。</summary>
    public int StartFrame { get; }

    /// <summary>窗口结束逻辑帧。</summary>
    public int EndFrame { get; }

    /// <summary>取消类型：Action 或 Movement。</summary>
    public CancelType CancelType { get; }

    /// <summary>允许触发 Action 取消的输入 id 列表。</summary>
    public string[] AllowedInputs { get; }

    /// <summary>同帧多个窗口命中时的优先级。</summary>
    public int Priority { get; }

    /// <summary>指定帧是否落在取消窗口内；窗口至少跨 1 帧才生效。</summary>
    public bool IsActiveAtFrame(int frame) =>
        EndFrame > StartFrame && frame >= StartFrame && frame <= EndFrame;
}

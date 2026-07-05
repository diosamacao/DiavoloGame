using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>帧窗口内输入可取消当前招式：下一招由 ActionResolverService 解析，或 Movement 取消。</summary>
[Serializable]
public class CancelWindow
{
    [SerializeField] int startFrame;
    [SerializeField] int endFrame;
    [SerializeField] CancelType cancelType = CancelType.Action;
    [Tooltip("CancelType.Action 时从 GameInputActions 选择允许的 Action；Movement 时忽略。")]
    [SerializeField] InputActionReference[] allowedInputs = Array.Empty<InputActionReference>();
    [SerializeField] int priority;

    public int StartFrame => startFrame;
    public int EndFrame => endFrame;
    public CancelType CancelType => cancelType;
    public int Priority => priority;

    /// <summary>解析后的输入 id 列表（Action 名）。</summary>
    public string[] AllowedInputs => InputBindingUtils.ResolveInputIds(allowedInputs);

    public bool IsActiveAtFrame(int frame) =>
        endFrame > startFrame && frame >= startFrame && frame <= endFrame;

    public ResolvedCancelWindow ToResolved() =>
        new(startFrame, endFrame, cancelType, AllowedInputs, priority);
}

/// <summary>运行时解析后的取消窗口。</summary>
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
        AllowedInputs = allowedInputIds ?? System.Array.Empty<string>();
        Priority = priority;
    }

    public int StartFrame { get; }
    public int EndFrame { get; }
    public CancelType CancelType { get; }
    public string[] AllowedInputs { get; }
    public int Priority { get; }

    public bool IsActiveAtFrame(int frame) =>
        EndFrame > StartFrame && frame >= StartFrame && frame <= EndFrame;
}

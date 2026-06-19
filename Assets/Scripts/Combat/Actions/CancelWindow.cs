using System;
using UnityEngine;

/// <summary>帧窗口内输入可取消当前招式：切到 targetAction 或移动取消。</summary>
[Serializable]
public class CancelWindow
{
    [SerializeField] int startFrame;
    [SerializeField] int endFrame;
    [SerializeField] CancelType cancelType = CancelType.Action;
    [SerializeField] string[] allowedInputs = { InputIds.Attack };
    [SerializeField] ActionDefinition targetAction;
    [SerializeField] int priority;

    public int StartFrame => startFrame;
    public int EndFrame => endFrame;
    public CancelType CancelType => cancelType;
    public string[] AllowedInputs => allowedInputs ?? Array.Empty<string>();
    public ActionDefinition TargetAction => targetAction;
    public int Priority => priority;

    public bool IsActiveAtFrame(int frame) =>
        endFrame > startFrame && frame >= startFrame && frame <= endFrame;

    public ResolvedCancelWindow ToResolved() =>
        new(startFrame, endFrame, cancelType, AllowedInputs, targetAction, priority);
}

/// <summary>运行时解析后的取消窗口。</summary>
public readonly struct ResolvedCancelWindow
{
    public ResolvedCancelWindow(
        int startFrame,
        int endFrame,
        CancelType cancelType,
        string[] allowedInputs,
        ActionDefinition targetAction,
        int priority)
    {
        StartFrame = startFrame;
        EndFrame = endFrame;
        CancelType = cancelType;
        AllowedInputs = allowedInputs ?? Array.Empty<string>();
        TargetAction = targetAction;
        Priority = priority;
    }

    public int StartFrame { get; }
    public int EndFrame { get; }
    public CancelType CancelType { get; }
    public string[] AllowedInputs { get; }
    public ActionDefinition TargetAction { get; }
    public int Priority { get; }

    public bool IsActiveAtFrame(int frame) =>
        EndFrame > StartFrame && frame >= StartFrame && frame <= EndFrame;
}

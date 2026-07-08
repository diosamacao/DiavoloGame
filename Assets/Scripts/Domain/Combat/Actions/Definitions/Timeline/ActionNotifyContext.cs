using UnityEngine;

/// <summary>统一 Notify 运行时上下文；Play Mode 与 ActionEditor Scrub 共用同一帧语义。</summary>
public readonly struct ActionNotifyContext
{
    public ActionNotifyContext(
        ActionDefinition action,
        int frameIndex,
        int previousFrameIndex,
        float elapsedSeconds,
        Transform actorRoot,
        Transform attachPoint,
        ActionNotify notify = null,
        ActionNotifyState state = null,
        ActionNotifyStatePhase statePhase = ActionNotifyStatePhase.Tick)
    {
        Action = action;
        FrameIndex = frameIndex;
        PreviousFrameIndex = previousFrameIndex;
        ElapsedSeconds = elapsedSeconds;
        ActorRoot = actorRoot;
        AttachPoint = attachPoint != null ? attachPoint : actorRoot;
        Notify = notify;
        State = state;
        StatePhase = statePhase;
    }

    /// <summary>当前触发时间轴事件的动作定义。</summary>
    public ActionDefinition Action { get; }

    /// <summary>当前逻辑帧。</summary>
    public int FrameIndex { get; }

    /// <summary>上一逻辑帧；首帧或 Scrub 起点时为 -1。</summary>
    public int PreviousFrameIndex { get; }

    /// <summary>动作已播放秒数。</summary>
    public float ElapsedSeconds { get; }

    /// <summary>攻击者或动作拥有者根 Transform。</summary>
    public Transform ActorRoot { get; }

    /// <summary>Notify 默认挂点；未绑定时回退到 ActorRoot。</summary>
    public Transform AttachPoint { get; }

    /// <summary>当前触发的点事件；区间事件时为空。</summary>
    public ActionNotify Notify { get; }

    /// <summary>当前触发的区间窗口；点事件时为空。</summary>
    public ActionNotifyState State { get; }

    /// <summary>区间窗口触发阶段；点事件时保持 Tick 默认值。</summary>
    public ActionNotifyStatePhase StatePhase { get; }
}

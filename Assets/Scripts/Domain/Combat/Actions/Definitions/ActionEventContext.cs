using UnityEngine;

/// <summary>ActionEvent 运行时派发上下文；Play Mode 与 ActionEditor Scrub 共用。</summary>
public readonly struct ActionEventContext
{
    public ActionEventContext(
        ActionDefinition action,
        ActionEvent actionEvent,
        int frameIndex,
        int previousFrameIndex,
        float elapsedSeconds,
        Transform actorRoot)
    {
        Action = action;
        Event = actionEvent;
        FrameIndex = frameIndex;
        PreviousFrameIndex = previousFrameIndex;
        ElapsedSeconds = elapsedSeconds;
        ActorRoot = actorRoot;
    }

    /// <summary>事件所属招式。</summary>
    public ActionDefinition Action { get; }

    /// <summary>被触发的时间轴事件。</summary>
    public ActionEvent Event { get; }

    /// <summary>当前逻辑帧。</summary>
    public int FrameIndex { get; }

    /// <summary>上一逻辑帧。</summary>
    public int PreviousFrameIndex { get; }

    /// <summary>招式已播放秒数。</summary>
    public float ElapsedSeconds { get; }

    /// <summary>攻击者根 Transform。</summary>
    public Transform ActorRoot { get; }
}

/// <summary>订阅 ActionEvent 轨道的运行时消费者。</summary>
public interface IActionEventConsumer
{
    /// <summary>ActionEvent 被逻辑帧派发时调用。</summary>
    void OnActionEvent(in ActionEventContext context);
}

using UnityEngine;

/// <summary>Runtime 整数动作帧上下文，供 Hitbox、Notify 与表现消费者读取。</summary>
public readonly struct CombatFrameContext
{
    /// <summary>创建由整数动作帧派生的 Runtime 帧上下文。</summary>
    public CombatFrameContext(
        ActionDefinition action,
        int frameIndex,
        int previousFrameIndex,
        Transform actorRoot)
    {
        Action = action;
        FrameIndex = frameIndex;
        PreviousFrameIndex = previousFrameIndex;
        ActorRoot = actorRoot;
    }

    /// <summary>当前招式 SO。</summary>
    public ActionDefinition Action { get; }

    /// <summary>当前逻辑帧（与 ActionDefinition.sampleRate 对齐）。</summary>
    public int FrameIndex { get; }

    /// <summary>上一逻辑帧；首帧或 Scrub 起点时为 -1。</summary>
    public int PreviousFrameIndex { get; }

    /// <summary>从整数动作帧派生的表现秒数；不得用于逻辑判断。</summary>
    public float ElapsedSeconds =>
        Action != null ? FrameIndex / (float)Action.SampleRate : 0f;

    /// <summary>攻击者根 Transform。</summary>
    public Transform ActorRoot { get; }
}

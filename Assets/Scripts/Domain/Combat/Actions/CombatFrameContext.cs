using UnityEngine;

/// <summary>Logic Tick 帧上下文：编辑器 Scrub 与 Play Mode 共用，供 Hitbox/VFX/Phase 消费者读取。</summary>
public readonly struct CombatFrameContext
{
    public CombatFrameContext(
        ActionDefinition action,
        int frameIndex,
        int previousFrameIndex,
        float elapsedSeconds,
        Transform actorRoot)
    {
        Action = action;
        FrameIndex = frameIndex;
        PreviousFrameIndex = previousFrameIndex;
        ElapsedSeconds = elapsedSeconds;
        ActorRoot = actorRoot;
    }

    /// <summary>当前招式 SO。</summary>
    public ActionDefinition Action { get; }

    /// <summary>当前逻辑帧（与 ActionDefinition.sampleRate 对齐）。</summary>
    public int FrameIndex { get; }

    /// <summary>上一逻辑帧；首帧或 Scrub 起点时为 -1。</summary>
    public int PreviousFrameIndex { get; }

    /// <summary>招式已播放秒数。</summary>
    public float ElapsedSeconds { get; }

    /// <summary>攻击者根 Transform。</summary>
    public Transform ActorRoot { get; }
}

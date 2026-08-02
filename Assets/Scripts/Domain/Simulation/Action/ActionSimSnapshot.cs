/// <summary>ActionSim 当前权威状态的只读值快照。</summary>
public readonly struct ActionSimSnapshot
{
    /// <summary>创建一份动作模拟状态快照。</summary>
    public ActionSimSnapshot(
        IActionSimContent content,
        IActionSimGraph graph,
        string nodeId,
        int currentFrame,
        int instanceId,
        bool hasConfirmedHit,
        bool isActive,
        int freezeFrames)
    {
        Content = content;
        Graph = graph;
        NodeId = nodeId;
        CurrentFrame = currentFrame;
        InstanceId = instanceId;
        HasConfirmedHit = hasConfirmedHit;
        IsActive = isActive;
        FreezeFrames = freezeFrames > 0 ? freezeFrames : 0;
    }

    /// <summary>当前动作内容；无活动动作时为空。</summary>
    public IActionSimContent Content { get; }

    /// <summary>当前动作图；直接动作可为空。</summary>
    public IActionSimGraph Graph { get; }

    /// <summary>当前图节点稳定 Id。</summary>
    public string NodeId { get; }

    /// <summary>当前权威动作帧。</summary>
    public int CurrentFrame { get; }

    /// <summary>当前动作实例的单调稳定 Id。</summary>
    public int InstanceId { get; }

    /// <summary>当前动作实例是否已确认至少一次命中。</summary>
    public bool HasConfirmedHit { get; }

    /// <summary>当前动作是否仍由模拟核持有。</summary>
    public bool IsActive { get; }

    /// <summary>剩余逻辑卡肉帧；大于 0 时不推进动作帧、不取运动表 Δ。</summary>
    public int FreezeFrames { get; }

    /// <summary>是否处于逻辑卡肉。</summary>
    public bool IsFrozen => FreezeFrames > 0;

    /// <summary>当前动作是否已到达 TotalFrames 终止哨兵。</summary>
    public bool IsComplete =>
        IsActive && Content != null && CurrentFrame >= Content.TotalFrames;
}

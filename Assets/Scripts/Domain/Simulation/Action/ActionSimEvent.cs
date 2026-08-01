/// <summary>ActionSim 输出的无 Unity 依赖动作事件。</summary>
public readonly struct ActionSimEvent
{
    /// <summary>创建动作事件并记录其稳定实例与帧身份。</summary>
    public ActionSimEvent(
        ActionSimEventType type,
        IActionSimContent content,
        IActionSimGraph graph,
        string nodeId,
        int frame,
        int previousFrame,
        int instanceId)
    {
        Type = type;
        Content = content;
        Graph = graph;
        NodeId = nodeId;
        Frame = frame;
        PreviousFrame = previousFrame;
        InstanceId = instanceId;
    }

    /// <summary>事件类型。</summary>
    public ActionSimEventType Type { get; }

    /// <summary>事件所属动作内容。</summary>
    public IActionSimContent Content { get; }

    /// <summary>事件发生时的动作图；直接动作为空。</summary>
    public IActionSimGraph Graph { get; }

    /// <summary>事件发生时的稳定图节点 Id。</summary>
    public string NodeId { get; }

    /// <summary>事件发生的动作帧。</summary>
    public int Frame { get; }

    /// <summary>跨帧派发前的动作帧；Started 时为 -1。</summary>
    public int PreviousFrame { get; }

    /// <summary>事件所属动作实例的稳定 Id。</summary>
    public int InstanceId { get; }
}

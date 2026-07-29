/// <summary>动作解析结果：最终招式，以及 Graph 解析时写入的图游标。</summary>
public readonly struct ActionResolveResult
{
    /// <summary>仅动作、无图游标（Single / Combo / Directional）。</summary>
    public static ActionResolveResult FromAction(ActionDefinition action) =>
        new(action, null, null);

    /// <summary>Graph 解析：动作 + 图游标（nodeId 为图内位置）。</summary>
    public static ActionResolveResult FromGraph(ActionDefinition action, ActionGraph graph, string nodeId) =>
        new(action, graph, nodeId);

    ActionResolveResult(ActionDefinition action, ActionGraph graph, string nodeId)
    {
        Action = action;
        Graph = graph;
        NodeId = nodeId;
    }

    /// <summary>要播放的招式。</summary>
    public ActionDefinition Action { get; }

    /// <summary>若由 ActionGraph 解析，则为所属图；否则 null。</summary>
    public ActionGraph Graph { get; }

    /// <summary>图内节点 id；无图游标时为 null。</summary>
    public string NodeId { get; }

    /// <summary>招式有效且可播放。</summary>
    public bool IsValid => Action != null && Action.HasAnimation;

    /// <summary>是否携带有效图游标（进入/停留在连招图内）。</summary>
    public bool HasGraphCursor => Graph != null && !string.IsNullOrEmpty(NodeId);

    /// <summary>图节点对应的玩法意图；直接播放动作时为 None。</summary>
    public GameplayIntentType Intent =>
        TryGetNode(out ActionGraphNode node) ? node.Intent : GameplayIntentType.None;

    /// <summary>解析结果携带图游标时返回对应节点。</summary>
    public bool TryGetNode(out ActionGraphNode node)
    {
        node = null;
        return HasGraphCursor && Graph.TryGetNode(NodeId, out node);
    }
}

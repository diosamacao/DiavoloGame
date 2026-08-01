/// <summary>纯模拟动作解析结果，携带内容、图游标与触发意图。</summary>
public readonly struct ActionSimResolveResult
{
    /// <summary>创建不带图游标的直接动作结果。</summary>
    public static ActionSimResolveResult FromContent(IActionSimContent content) =>
        new ActionSimResolveResult(content, null, null, GameplayIntentType.None);

    /// <summary>创建带图游标的动作结果。</summary>
    public static ActionSimResolveResult FromGraph(
        IActionSimContent content,
        IActionSimGraph graph,
        string nodeId,
        GameplayIntentType intent = GameplayIntentType.None) =>
        new ActionSimResolveResult(content, graph, nodeId, intent);

    /// <summary>初始化完整解析结果。</summary>
    public ActionSimResolveResult(
        IActionSimContent content,
        IActionSimGraph graph,
        string nodeId,
        GameplayIntentType intent)
    {
        Content = content;
        Graph = graph;
        NodeId = nodeId;
        Intent = intent;
    }

    /// <summary>解析得到的动作内容。</summary>
    public IActionSimContent Content { get; }

    /// <summary>解析发生的动作图；直接动作可为空。</summary>
    public IActionSimGraph Graph { get; }

    /// <summary>解析后的图节点稳定 Id。</summary>
    public string NodeId { get; }

    /// <summary>触发本次解析的玩法意图。</summary>
    public GameplayIntentType Intent { get; }

    /// <summary>结果是否包含已完成 60Hz 迁移的有效动作内容。</summary>
    public bool IsValid => Content != null && Content.IsSimulationReady;

    /// <summary>结果是否携带完整图游标。</summary>
    public bool HasGraphCursor => Graph != null && !string.IsNullOrEmpty(NodeId);
}

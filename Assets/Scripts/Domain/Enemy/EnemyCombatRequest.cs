/// <summary>敌人一帧战斗起手请求：显式 Graph Entry（不经 Attack Intent）。</summary>
public readonly struct EnemyCombatRequest
{
    /// <summary>空请求。</summary>
    public static EnemyCombatRequest None => default;

    /// <summary>创建指定 Entry 起手请求；entryNodeId 须为 ActiveGraph 的 Entry。</summary>
    public EnemyCombatRequest(string entryNodeId)
    {
        EntryNodeId = entryNodeId ?? string.Empty;
        HasRequest = !string.IsNullOrEmpty(EntryNodeId);
    }

    /// <summary>本帧是否有起手请求。</summary>
    public bool HasRequest { get; }

    /// <summary>ActionGraph Entry 的 NodeId。</summary>
    public string EntryNodeId { get; }
}

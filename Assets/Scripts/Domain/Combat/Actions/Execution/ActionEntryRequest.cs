/// <summary>角色一帧离散招式请求：显式指定当前 ActionGraph 的 Entry。</summary>
public readonly struct ActionEntryRequest
{
    /// <summary>空请求。</summary>
    public static ActionEntryRequest None => default;

    /// <summary>创建指定 Entry 起手请求。</summary>
    public ActionEntryRequest(string entryNodeId)
    {
        EntryNodeId = entryNodeId ?? string.Empty;
        HasRequest = !string.IsNullOrEmpty(EntryNodeId);
    }

    /// <summary>本帧是否有有效请求。</summary>
    public bool HasRequest { get; }

    /// <summary>ActionGraph Entry NodeId。</summary>
    public string EntryNodeId { get; }
}

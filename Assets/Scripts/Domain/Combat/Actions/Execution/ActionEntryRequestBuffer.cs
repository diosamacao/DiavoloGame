/// <summary>招式 Entry 请求帧槽；控制方写入，CharacterActionDriver 单次消费。</summary>
public sealed class ActionEntryRequestBuffer : IActionEntryRequestSource
{
    ActionEntryRequest _pending;

    /// <summary>当前未消费请求（调试）。</summary>
    public ActionEntryRequest Pending => _pending;

    /// <summary>是否仍有待消费请求。</summary>
    public bool HasPending => _pending.HasRequest;

    /// <summary>提交本帧请求（覆盖同帧多次写入）。</summary>
    public void Set(in ActionEntryRequest request) => _pending = request;

    /// <summary>读取但不清除。</summary>
    public bool TryPeek(out ActionEntryRequest request)
    {
        request = _pending;
        return _pending.HasRequest;
    }

    /// <inheritdoc />
    public bool TryConsume(out ActionEntryRequest request)
    {
        request = _pending;
        _pending = ActionEntryRequest.None;
        return request.HasRequest;
    }

    /// <summary>清空槽位。</summary>
    public void Clear() => _pending = ActionEntryRequest.None;
}

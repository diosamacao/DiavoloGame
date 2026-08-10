/// <summary>
/// 敌人战斗请求帧槽：Brain 在 ProduceInput 写入，Driver 在 Actor.Step 消费。
/// </summary>
public sealed class EnemyCombatRequestBuffer
{
    EnemyCombatRequest _pending;

    /// <summary>当前未消费请求（调试/HUD）。</summary>
    public EnemyCombatRequest Pending => _pending;

    /// <summary>是否仍有待消费请求。</summary>
    public bool HasPending => _pending.HasRequest;

    /// <summary>Brain 提交本帧请求（覆盖同帧多次写入）。</summary>
    public void Set(in EnemyCombatRequest request) => _pending = request;

    /// <summary>读取但不清除。</summary>
    public bool TryPeek(out EnemyCombatRequest request)
    {
        request = _pending;
        return _pending.HasRequest;
    }

    /// <summary>取出并清空（Driver 消费）。</summary>
    public bool TryConsume(out EnemyCombatRequest request)
    {
        request = _pending;
        _pending = EnemyCombatRequest.None;
        return request.HasRequest;
    }

    /// <summary>清空槽位。</summary>
    public void Clear() => _pending = EnemyCombatRequest.None;
}

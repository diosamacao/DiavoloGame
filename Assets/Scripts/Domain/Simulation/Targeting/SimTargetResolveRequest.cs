/// <summary>一次确定性目标维护/切换请求的完整输入。</summary>
public readonly struct SimTargetResolveRequest
{
    /// <summary>创建目标解析请求；距离单位均为毫米。</summary>
    public SimTargetResolveRequest(
        SimActorId requesterId,
        int requesterTeamId,
        int originXMm,
        int originZMm,
        ushort moveReferenceYawQuantized,
        int acquireRangeMm,
        int retainRangeMm,
        SimActorId currentSelectedTargetId,
        TargetSwitchDirection switchDirection)
    {
        RequesterId = requesterId;
        RequesterTeamId = requesterTeamId;
        OriginXMm = originXMm;
        OriginZMm = originZMm;
        MoveReferenceYawQuantized = moveReferenceYawQuantized;
        AcquireRangeMm = acquireRangeMm;
        RetainRangeMm = retainRangeMm;
        CurrentSelectedTargetId = currentSelectedTargetId;
        SwitchDirection = switchDirection;
    }

    /// <summary>发起解析的角色身份。</summary>
    public SimActorId RequesterId { get; }
    /// <summary>发起者阵营。</summary>
    public int RequesterTeamId { get; }
    /// <summary>发起者逻辑根 X，单位毫米。</summary>
    public int OriginXMm { get; }
    /// <summary>发起者逻辑根 Z，单位毫米。</summary>
    public int OriginZMm { get; }
    /// <summary>切敌方位参考偏航，单位 0.1 度。</summary>
    public ushort MoveReferenceYawQuantized { get; }
    /// <summary>自动选中与显式切敌可使用的范围。</summary>
    public int AcquireRangeMm { get; }
    /// <summary>当前目标继续保持有效的范围。</summary>
    public int RetainRangeMm { get; }
    /// <summary>进入本帧前的唯一已选目标。</summary>
    public SimActorId CurrentSelectedTargetId { get; }
    /// <summary>本帧 Pressed 边沿解析出的切换方向。</summary>
    public TargetSwitchDirection SwitchDirection { get; }
}

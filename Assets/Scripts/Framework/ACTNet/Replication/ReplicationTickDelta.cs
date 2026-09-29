using System;

/// <summary>一次纯准备产生的生命周期与快照发送包。</summary>
public sealed class ReplicationTickDelta
{
    /// <summary>创建不可变准备结果。</summary>
    public ReplicationTickDelta(PreparedReplicationPacket[] packets) => Packets = packets ?? Array.Empty<PreparedReplicationPacket>();
    /// <summary>按生命周期在前、快照在后的稳定发送顺序。</summary>
    public PreparedReplicationPacket[] Packets { get; }
}

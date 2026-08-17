using System;

/// <summary>描述客户端必须显式移除的复制实体。</summary>
public sealed class DespawnRecord
{
    /// <summary>待移除实体的稳定网络标识。</summary>
    public NetEntityId EntityId { get; }

    /// <summary>创建一条有效的 Despawn 记录。</summary>
    public DespawnRecord(NetEntityId entityId)
    {
        if (!entityId.IsValid)
            throw new ArgumentException("Despawn EntityId 必须有效。", nameof(entityId));
        EntityId = entityId;
    }
}

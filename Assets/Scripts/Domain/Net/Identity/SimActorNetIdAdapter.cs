using System;

/// <summary>在 ACTGame Simulation 身份与通用网络实体身份之间建立显式边界映射。</summary>
public static class SimActorNetIdAdapter
{
    /// <summary>将有效 SimActorId 映射为同数值 NetEntityId。</summary>
    public static NetEntityId ToNetEntityId(SimActorId actorId)
    {
        if (!actorId.IsValid)
            throw new ArgumentException("不能映射无效 SimActorId。", nameof(actorId));
        return new NetEntityId(actorId.Value);
    }

    /// <summary>将有效 NetEntityId 映射为同数值 SimActorId。</summary>
    public static SimActorId ToSimActorId(NetEntityId entityId)
    {
        if (!entityId.IsValid)
            throw new ArgumentException("不能映射无效 NetEntityId。", nameof(entityId));
        return new SimActorId(entityId.Value);
    }
}

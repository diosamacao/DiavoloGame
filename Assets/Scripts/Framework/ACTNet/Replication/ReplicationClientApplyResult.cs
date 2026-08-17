using System;

/// <summary>向后续 ACT Adapter 暴露一次帧应用的顺序化记录与拒绝原因。</summary>
public sealed class ReplicationClientApplyResult
{
    readonly SpawnRecord[] _spawns;
    readonly EntityRecord[] _updates;
    readonly DespawnRecord[] _despawns;

    /// <summary>帧应用的最终状态。</summary>
    public ReplicationClientApplyStatus Status { get; }

    /// <summary>被处理帧的序列号。</summary>
    public NetSequence Sequence { get; }

    /// <summary>拒绝操作的明确注册表结果；非生命周期错误时为 Success。</summary>
    public ReplicatedEntityOperationResult OperationResult { get; }

    /// <summary>拒绝或丢弃原因；成功时为空字符串。</summary>
    public string Message { get; }

    /// <summary>成功应用时按 EntityId 排序的 Spawn 记录副本。</summary>
    public SpawnRecord[] Spawns => (SpawnRecord[])_spawns.Clone();

    /// <summary>成功应用时按 EntityId 排序的 Update 记录副本。</summary>
    public EntityRecord[] Updates => (EntityRecord[])_updates.Clone();

    /// <summary>成功应用时按 EntityId 排序的 Despawn 记录副本。</summary>
    public DespawnRecord[] Despawns => (DespawnRecord[])_despawns.Clone();

    /// <summary>创建一份不可变的帧应用结果。</summary>
    public ReplicationClientApplyResult(
        ReplicationClientApplyStatus status,
        NetSequence sequence,
        ReplicatedEntityOperationResult operationResult,
        string message,
        SpawnRecord[] spawns,
        EntityRecord[] updates,
        DespawnRecord[] despawns)
    {
        Status = status;
        Sequence = sequence;
        OperationResult = operationResult;
        Message = message ?? string.Empty;
        _spawns = spawns == null ? Array.Empty<SpawnRecord>() : (SpawnRecord[])spawns.Clone();
        _updates = updates == null ? Array.Empty<EntityRecord>() : (EntityRecord[])updates.Clone();
        _despawns =
            despawns == null ? Array.Empty<DespawnRecord>() : (DespawnRecord[])despawns.Clone();
    }
}

using System;

/// <summary>按严格递增 Sequence 原子应用显式实体生命周期帧。</summary>
public sealed class ReplicationClient
{
    readonly ReplicationSchemaRegistry _schemas;
    readonly ReplicatedEntityRegistry _registry = new ReplicatedEntityRegistry();
    NetSequence _latestSequence = NetSequence.Invalid;

    /// <summary>在成功帧提交后按 Spawn → Update → Despawn 顺序通知记录消费者。</summary>
    public event Action<SpawnRecord> Spawned;

    /// <summary>在成功帧提交后按 Spawn → Update → Despawn 顺序通知记录消费者。</summary>
    public event Action<EntityRecord> Updated;

    /// <summary>在成功帧提交后按 Spawn → Update → Despawn 顺序通知记录消费者。</summary>
    public event Action<DespawnRecord> Despawned;

    /// <summary>创建必须通过 Schema Registry 验证状态载荷的复制客户端。</summary>
    public ReplicationClient(ReplicationSchemaRegistry schemas)
    {
        _schemas = schemas ?? throw new ArgumentNullException(nameof(schemas));
    }

    /// <summary>返回当前活动实体注册表的独立快照。</summary>
    public ReplicatedEntityRegistry Registry => _registry.Clone();

    /// <summary>最后一个成功应用的帧序列；尚未应用时为 Invalid。</summary>
    public NetSequence LatestSequence => _latestSequence;

    /// <summary>丢掉已应用实体，保留 LatestSequence，供 baseline 恢复后重新 Spawn。</summary>
    public void ResetRegistry()
    {
        _registry.ReplaceWith(new ReplicatedEntityRegistry());
    }

    /// <summary>应用严格更新的帧；旧或重复 Sequence 整帧丢弃且不触发事件。</summary>
    public ReplicationClientApplyResult ApplyFrame(ReplicationFrame frame)
    {
        if (frame == null)
            throw new ArgumentNullException(nameof(frame));

        if (_latestSequence.IsValid && frame.Sequence.CompareTo(_latestSequence) <= 0)
        {
            return BuildResult(
                ReplicationClientApplyStatus.StaleSequence,
                frame,
                ReplicatedEntityOperationResult.Success,
                $"Sequence {frame.Sequence} 不严格新于 {_latestSequence}。",
                includeRecords: false);
        }

        ReplicatedEntityRegistry nextRegistry = _registry.Clone();
        ReplicationClientApplyResult rejected = ValidateAndApply(frame, nextRegistry);
        if (rejected != null)
            return rejected;

        // 完整帧验证成功后一次提交；普通帧未提及的实体不会被推断为 Despawn。
        _registry.ReplaceWith(nextRegistry);
        _latestSequence = frame.Sequence;
        Publish(frame);
        return BuildResult(
            ReplicationClientApplyStatus.Applied,
            frame,
            ReplicatedEntityOperationResult.Success,
            string.Empty,
            includeRecords: true);
    }

    // 在临时 Registry 上依序验证 payload 与 Spawn/Update/Despawn，任一失败即放弃整帧。
    ReplicationClientApplyResult ValidateAndApply(
        ReplicationFrame frame,
        ReplicatedEntityRegistry nextRegistry)
    {
        for (int i = 0; i < frame.SpawnBuffer.Length; i++)
        {
            SpawnRecord record = frame.SpawnBuffer[i];
            string payloadError = ValidatePayload(record.SchemaId, record.PayloadBuffer);
            if (payloadError != null)
                return Rejected(frame, payloadError);

            ReplicatedEntityOperationResult result = nextRegistry.TrySpawn(
                record.EntityId,
                record.ArchetypeId,
                record.SchemaId);
            if (result != ReplicatedEntityOperationResult.Success)
                return Rejected(frame, result, "Spawn", record.EntityId);
        }

        for (int i = 0; i < frame.UpdateBuffer.Length; i++)
        {
            EntityRecord record = frame.UpdateBuffer[i];
            string payloadError = ValidatePayload(record.SchemaId, record.PayloadBuffer);
            if (payloadError != null)
                return Rejected(frame, payloadError);

            ReplicatedEntityOperationResult result =
                nextRegistry.TryUpdate(record.EntityId, record.SchemaId);
            if (result != ReplicatedEntityOperationResult.Success)
                return Rejected(frame, result, "Update", record.EntityId);
        }

        for (int i = 0; i < frame.DespawnBuffer.Length; i++)
        {
            DespawnRecord record = frame.DespawnBuffer[i];
            ReplicatedEntityOperationResult result =
                nextRegistry.TryDespawn(record.EntityId);
            if (result != ReplicatedEntityOperationResult.Success)
                return Rejected(frame, result, "Despawn", record.EntityId);
        }

        return null;
    }

    // 将业务 Schema 的缺失或格式异常转换为可返回给 Adapter 的拒绝原因。
    string ValidatePayload(ushort schemaId, byte[] payload)
    {
        try
        {
            _schemas.Decode(schemaId, payload);
            return null;
        }
        catch (Exception ex)
        {
            return $"Schema {schemaId} 拒绝 payload：{ex.Message}";
        }
    }

    // 提交后才发布记录，避免消费者观察到最终被拒绝的半帧状态。
    void Publish(ReplicationFrame frame)
    {
        for (int i = 0; i < frame.SpawnBuffer.Length; i++)
            Spawned?.Invoke(frame.SpawnBuffer[i]);
        for (int i = 0; i < frame.UpdateBuffer.Length; i++)
            Updated?.Invoke(frame.UpdateBuffer[i]);
        for (int i = 0; i < frame.DespawnBuffer.Length; i++)
            Despawned?.Invoke(frame.DespawnBuffer[i]);
    }

    static ReplicationClientApplyResult Rejected(
        ReplicationFrame frame,
        string message) =>
        BuildResult(
            ReplicationClientApplyStatus.Rejected,
            frame,
            ReplicatedEntityOperationResult.Success,
            message,
            includeRecords: false);

    static ReplicationClientApplyResult Rejected(
        ReplicationFrame frame,
        ReplicatedEntityOperationResult result,
        string operation,
        NetEntityId entityId) =>
        BuildResult(
            ReplicationClientApplyStatus.Rejected,
            frame,
            result,
            $"{operation} Entity {entityId} 被拒绝：{result}。",
            includeRecords: false);

    static ReplicationClientApplyResult BuildResult(
        ReplicationClientApplyStatus status,
        ReplicationFrame frame,
        ReplicatedEntityOperationResult operationResult,
        string message,
        bool includeRecords) =>
        new ReplicationClientApplyResult(
            status,
            frame.Sequence,
            operationResult,
            message,
            includeRecords ? frame.SpawnBuffer : Array.Empty<SpawnRecord>(),
            includeRecords ? frame.UpdateBuffer : Array.Empty<EntityRecord>(),
            includeRecords ? frame.DespawnBuffer : Array.Empty<DespawnRecord>());
}

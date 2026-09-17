using System;

/// <summary>把 ACTGame 角色整数时钟快照接入通用复制 Registry 的 V2 业务 Schema。</summary>
public sealed class CharacterSnapshotSchemaV2 : IReplicationSchema
{
    /// <summary>角色快照 V2 的 Schema 标识。</summary>
    public const ushort Id = 2;

    /// <summary>返回角色快照 V2 Schema 标识。</summary>
    public ushort SchemaId => Id;

    /// <summary>编码强类型角色快照，并复用 Simulation 中的唯一字段布局。</summary>
    public byte[] Encode(in ActorReplicationSnapshot snapshot) =>
        ActorReplicationSnapshotCodec.Encode(in snapshot);

    /// <summary>仅接受 ActorReplicationSnapshot；null 或其它业务类型会明确抛错。</summary>
    public byte[] Encode(object state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        if (!(state is ActorReplicationSnapshot snapshot))
        {
            throw new ArgumentException(
                $"CharacterSnapshotSchemaV2 仅接受 {nameof(ActorReplicationSnapshot)}。",
                nameof(state));
        }

        return Encode(in snapshot);
    }

    /// <summary>严格解码强类型角色快照；截断和尾随字节均由唯一 Codec 拒绝。</summary>
    public ActorReplicationSnapshot DecodeSnapshot(byte[] payload) =>
        ActorReplicationSnapshotCodec.Decode(payload);

    /// <summary>解码 Registry 传入的完整载荷并返回 ActorReplicationSnapshot 装箱值。</summary>
    public object Decode(byte[] payload) => DecodeSnapshot(payload);
}

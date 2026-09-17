using System;
using UnityEngine;

/// <summary>ACT 角色生产 Schema：统一 CharacterActor Capture 与 V2 整数时钟编解码入口。</summary>
public sealed class ActCharacterSnapshotSchema : IReplicationSchema
{
    readonly ActContentRegistry _content;
    readonly CharacterSnapshotSchemaV2 _wireSchema = new();

    /// <summary>ACT 角色快照 V2 Schema Id。</summary>
    public const ushort Id = CharacterSnapshotSchemaV2.Id;

    /// <summary>创建绑定当前房间内容 Registry 的角色生产 Schema。</summary>
    public ActCharacterSnapshotSchema(ActContentRegistry content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    /// <summary>返回角色快照 V2 Schema Id。</summary>
    public ushort SchemaId => Id;

    /// <summary>
    /// 从权威 CharacterActor 捕获最小复制集。
    /// 读取 Vitality 边沿，不读取 CameraLock、Look 或 Lean 表现状态。
    /// </summary>
    public ActorReplicationSnapshot Capture(
        CharacterActor actor,
        ReplicationActorKind kind = ReplicationActorKind.Player)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));

        ActionSimSnapshot action = actor.ActionSim.Snapshot;
        int actionId = 0;
        if (action.IsActive && action.Content is ActionDefinition definition)
            actionId = _content.Actions.GetOrAdd(definition);

        byte locomotionPhase = 0;
        int locomotionPhaseFrame = 0;
        if (!action.IsActive && actor.Locomotion != null)
        {
            LocomotionSavedState locomotion = actor.Locomotion.Capture();
            locomotionPhase = (byte)locomotion.AnimationKey;
            locomotionPhaseFrame = ResolveReplicatedPhaseFrame(
                locomotion.AnimationKey,
                locomotion.PhaseFrame);
        }

        int healthMilli = actor.Numeric != null
            ? actor.Numeric.Attributes.GetCurrent(AttributeId.Health)
            : 0;

        // Wish 单位方向与位姿使用同一 Tick，供 Observer 朝向调试与表现恢复。
        PackWishDirection(
            actor.DebugMoveWishWorldDirection,
            out int moveVxMm,
            out int moveVzMm);

        return ReplicationSnapshotBuilder.FromAuthority(
            actor.SimulationId,
            actor.TeamId,
            kind,
            actor.MotorSim,
            in action,
            actionId,
            actor.TargetingSnapshot.SelectedTargetId,
            healthMilli,
            flagsPacked: PartyReplicationPacking.WithMemberState(0, actor.PartyState),
            actor.Vitality != null
                ? actor.Vitality.ReplicationEdge
                : VitalityReplicationEdge.None,
            moveVxMm,
            moveVzMm,
            locomotionPhase: locomotionPhase,
            gait: (byte)actor.ReplicationGait,
            cardinal: actor.ReplicationCardinal,
            locomotionPhaseFrame: locomotionPhaseFrame);
    }

    /// <summary>编码强类型角色快照，并复用纯 C# V2 Schema 的唯一线格式。</summary>
    public byte[] Encode(in ActorReplicationSnapshot snapshot) =>
        _wireSchema.Encode(in snapshot);

    /// <summary>编码 Registry 传入的角色状态对象；非法类型由 V2 Schema 明确拒绝。</summary>
    public byte[] Encode(object state) => _wireSchema.Encode(state);

    /// <summary>严格解码强类型角色快照。</summary>
    public ActorReplicationSnapshot DecodeSnapshot(byte[] payload) =>
        _wireSchema.DecodeSnapshot(payload);

    /// <summary>解码 Registry 传入的完整载荷。</summary>
    public object Decode(byte[] payload) => _wireSchema.Decode(payload);

    /// <summary>Idle 在线上固定相位 0；其余相位保留权威整数帧。</summary>
    public static int ResolveReplicatedPhaseFrame(AnimationKey key, int phaseFrame) =>
        key == AnimationKey.Idle ? 0 : phaseFrame;

    /// <summary>把水平 Wish 写成毫米单位方向；无输入时保持 0。</summary>
    static void PackWishDirection(Vector3 wishWorld, out int moveVxMm, out int moveVzMm)
    {
        wishWorld.y = 0f;
        if (wishWorld.sqrMagnitude < 0.0001f)
        {
            moveVxMm = 0;
            moveVzMm = 0;
            return;
        }

        Vector3 normalized = wishWorld.normalized;
        moveVxMm = MotionQuantization.MetersToMm(normalized.x);
        moveVzMm = MotionQuantization.MetersToMm(normalized.z);
    }

}

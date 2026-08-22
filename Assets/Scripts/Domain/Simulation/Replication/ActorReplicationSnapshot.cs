using System;

/// <summary>
/// 单 Actor 权威状态最小复制集。不含 CameraLock / Look / Lean 等本地表现。
/// </summary>
public readonly struct ActorReplicationSnapshot : IEquatable<ActorReplicationSnapshot>
{
    /// <summary>创建一份完整复制快照；selectedTargetId 仅 Owner 有意义，他人用 Invalid。</summary>
    public ActorReplicationSnapshot(
        SimActorId actorId,
        int teamId,
        ReplicationActorKind kind,
        int posXMm,
        int posZMm,
        int posYMm,
        int facingMilliDeg,
        int moveVxMm,
        int moveVzMm,
        byte locomotionPhase,
        byte gait,
        byte cardinal,
        int actionId,
        int graphNodeKey,
        int actionFrame,
        int freezeFrames,
        SimActorId selectedTargetId,
        int healthMilli,
        int flagsPacked,
        VitalityReplicationEdge vitalityEdge,
        ushort locomotionNormalizedMilli = 0)
    {
        ActorId = actorId;
        TeamId = teamId;
        Kind = kind;
        PosXMm = posXMm;
        PosZMm = posZMm;
        PosYMm = posYMm;
        FacingMilliDeg = facingMilliDeg;
        MoveVxMm = moveVxMm;
        MoveVzMm = moveVzMm;
        LocomotionPhase = locomotionPhase;
        Gait = gait;
        Cardinal = cardinal;
        LocomotionNormalizedMilli = locomotionNormalizedMilli;
        ActionId = actionId;
        GraphNodeKey = graphNodeKey;
        ActionFrame = actionFrame < 0 ? 0 : actionFrame;
        FreezeFrames = freezeFrames < 0 ? 0 : freezeFrames;
        SelectedTargetId = selectedTargetId;
        HealthMilli = healthMilli;
        FlagsPacked = flagsPacked;
        VitalityEdge = vitalityEdge;
    }

    /// <summary>稳定模拟身份。</summary>
    public SimActorId ActorId { get; }

    /// <summary>阵营 Id。</summary>
    public int TeamId { get; }

    /// <summary>玩家或敌人。</summary>
    public ReplicationActorKind Kind { get; }

    /// <summary>世界 X（毫米）。</summary>
    public int PosXMm { get; }

    /// <summary>世界 Z（毫米）。</summary>
    public int PosZMm { get; }

    /// <summary>世界 Y（毫米）。</summary>
    public int PosYMm { get; }

    /// <summary>绕 Y 朝向（毫度）。</summary>
    public int FacingMilliDeg { get; }

    /// <summary>水平速度 X（毫米/秒），供远端插值；P0 可为 0。</summary>
    public int MoveVxMm { get; }

    /// <summary>水平速度 Z（毫米/秒），供远端插值；P0 可为 0。</summary>
    public int MoveVzMm { get; }

    /// <summary>Locomotion 相位编码；P0 可为 0。</summary>
    public byte LocomotionPhase { get; }

    /// <summary>步态编码；P0 可为 0。</summary>
    public byte Gait { get; }

    /// <summary>八向 cardinal 编码；P0 可为 0。</summary>
    public byte Cardinal { get; }

    /// <summary>Locomotion Clip 归一化时间 ×1000；循环片可大于 1000。幽灵按此 Seek。</summary>
    public ushort LocomotionNormalizedMilli { get; }

    /// <summary>权威动作定义 Id；0 表示无活动动作。</summary>
    public int ActionId { get; }

    /// <summary>当前 Graph 节点稳定整数键；无动作时为 0。</summary>
    public int GraphNodeKey { get; }

    /// <summary>权威动作帧。</summary>
    public int ActionFrame { get; }

    /// <summary>剩余逻辑卡肉帧。</summary>
    public int FreezeFrames { get; }

    /// <summary>选中目标；非 Owner 或无目标时为 Invalid。</summary>
    public SimActorId SelectedTargetId { get; }

    /// <summary>当前生命（毫点）。</summary>
    public int HealthMilli { get; }

    /// <summary>战斗 Flags 打包位；P0 可为 0。</summary>
    public int FlagsPacked { get; }

    /// <summary>本 Tick 生命边沿。</summary>
    public VitalityReplicationEdge VitalityEdge { get; }

    /// <summary>用预测动作 Id/帧替换本快照的出招字段；位姿与生命不变。</summary>
    public ActorReplicationSnapshot WithAction(int actionId, int actionFrame, int freezeFrames = 0)
    {
        return new ActorReplicationSnapshot(
            ActorId,
            TeamId,
            Kind,
            PosXMm,
            PosZMm,
            PosYMm,
            FacingMilliDeg,
            MoveVxMm,
            MoveVzMm,
            LocomotionPhase,
            Gait,
            Cardinal,
            actionId,
            GraphNodeKey,
            actionFrame < 0 ? 0 : actionFrame,
            freezeFrames < 0 ? 0 : freezeFrames,
            SelectedTargetId,
            HealthMilli,
            FlagsPacked,
            VitalityEdge,
            LocomotionNormalizedMilli);
    }

    /// <summary>替换 Locomotion 相位与归一化时间；供客机本地走跑表现，不改位姿/出招。</summary>
    public ActorReplicationSnapshot WithLocomotion(byte locomotionPhase, ushort locomotionNormalizedMilli)
    {
        return new ActorReplicationSnapshot(
            ActorId,
            TeamId,
            Kind,
            PosXMm,
            PosZMm,
            PosYMm,
            FacingMilliDeg,
            MoveVxMm,
            MoveVzMm,
            locomotionPhase,
            Gait,
            Cardinal,
            ActionId,
            GraphNodeKey,
            ActionFrame,
            FreezeFrames,
            SelectedTargetId,
            HealthMilli,
            FlagsPacked,
            VitalityEdge,
            locomotionNormalizedMilli);
    }

    /// <summary>用预测电机位姿替换本快照的毫米坐标与朝向；其它复制字段不变。</summary>
    public ActorReplicationSnapshot WithMotorPose(CharacterMotorSim motor)
    {
        if (motor == null)
            throw new ArgumentNullException(nameof(motor));

        return new ActorReplicationSnapshot(
            ActorId,
            TeamId,
            Kind,
            motor.PositionMm.X,
            motor.PositionMm.Z,
            motor.YMm,
            motor.FacingMilliDeg,
            MoveVxMm,
            MoveVzMm,
            LocomotionPhase,
            Gait,
            Cardinal,
            ActionId,
            GraphNodeKey,
            ActionFrame,
            FreezeFrames,
            SelectedTargetId,
            HealthMilli,
            FlagsPacked,
            VitalityEdge,
            LocomotionNormalizedMilli);
    }

    /// <summary>比较全部复制字段。</summary>
    public bool Equals(ActorReplicationSnapshot other) =>
        ActorId == other.ActorId
        && TeamId == other.TeamId
        && Kind == other.Kind
        && PosXMm == other.PosXMm
        && PosZMm == other.PosZMm
        && PosYMm == other.PosYMm
        && FacingMilliDeg == other.FacingMilliDeg
        && MoveVxMm == other.MoveVxMm
        && MoveVzMm == other.MoveVzMm
        && LocomotionPhase == other.LocomotionPhase
        && Gait == other.Gait
        && Cardinal == other.Cardinal
        && LocomotionNormalizedMilli == other.LocomotionNormalizedMilli
        && ActionId == other.ActionId
        && GraphNodeKey == other.GraphNodeKey
        && ActionFrame == other.ActionFrame
        && FreezeFrames == other.FreezeFrames
        && SelectedTargetId == other.SelectedTargetId
        && HealthMilli == other.HealthMilli
        && FlagsPacked == other.FlagsPacked
        && VitalityEdge == other.VitalityEdge;

    /// <inheritdoc />
    public override bool Equals(object obj) =>
        obj is ActorReplicationSnapshot other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = ActorId.GetHashCode();
            hash = (hash * 397) ^ TeamId;
            hash = (hash * 397) ^ (int)Kind;
            hash = (hash * 397) ^ PosXMm;
            hash = (hash * 397) ^ PosZMm;
            hash = (hash * 397) ^ PosYMm;
            hash = (hash * 397) ^ FacingMilliDeg;
            hash = (hash * 397) ^ LocomotionNormalizedMilli;
            hash = (hash * 397) ^ ActionId;
            hash = (hash * 397) ^ GraphNodeKey;
            hash = (hash * 397) ^ ActionFrame;
            hash = (hash * 397) ^ HealthMilli;
            return hash;
        }
    }
}

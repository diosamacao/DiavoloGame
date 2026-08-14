using System;

/// <summary>
/// 从权威 Motor / Action 快照与已提取的数值字段组装复制最小集。
/// Numeric/Vitality 不引入本程序集，由调用方传入 healthMilli / flags / edge。
/// </summary>
public static class ReplicationSnapshotBuilder
{
    /// <summary>
    /// 用电机位姿与动作快照填充复制字段；actionId 由调用方映射（内容接口无稳定 int Id）。
    /// </summary>
    public static ActorReplicationSnapshot FromAuthority(
        SimActorId actorId,
        int teamId,
        ReplicationActorKind kind,
        CharacterMotorSim motor,
        in ActionSimSnapshot action,
        int actionId,
        SimActorId selectedTargetId,
        int healthMilli,
        int flagsPacked,
        VitalityReplicationEdge vitalityEdge,
        int moveVxMm = 0,
        int moveVzMm = 0,
        byte locomotionPhase = 0,
        byte gait = 0,
        byte cardinal = 0,
        ushort locomotionNormalizedMilli = 0)
    {
        if (motor == null)
            throw new ArgumentNullException(nameof(motor));

        SimVec2 pos = motor.PositionMm;
        int resolvedActionId = action.IsActive ? actionId : 0;
        string nodeId = action.IsActive ? action.NodeId ?? string.Empty : string.Empty;
        int actionFrame = action.IsActive ? action.CurrentFrame : 0;
        int freeze = action.IsActive ? action.FreezeFrames : 0;

        return new ActorReplicationSnapshot(
            actorId,
            teamId,
            kind,
            pos.X,
            pos.Z,
            motor.YMm,
            motor.FacingMilliDeg,
            moveVxMm,
            moveVzMm,
            locomotionPhase,
            gait,
            cardinal,
            resolvedActionId,
            nodeId,
            actionFrame,
            freeze,
            selectedTargetId,
            healthMilli,
            flagsPacked,
            vitalityEdge,
            locomotionNormalizedMilli);
    }
}

using System;

/// <summary>从权威 CharacterActor 组装下行快照；不读 CameraLock / Look / Lean。</summary>
public static class CharacterReplicationCapture
{
    /// <summary>
    /// 用 Motor / Action / Numeric / 动画键填充最小复制集。
    /// actionId 写入共享 Catalog；空闲时 locomotionPhase 为当前 AnimationKey。
    /// </summary>
    public static ActorReplicationSnapshot FromActor(
        CharacterActor actor,
        ActionReplicationCatalog catalog,
        ReplicationActorKind kind = ReplicationActorKind.Player)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));
        if (catalog == null)
            throw new ArgumentNullException(nameof(catalog));

        ActionSimSnapshot action = actor.ActionSim.Snapshot;
        int actionId = 0;
        if (action.IsActive && action.Content is ActionDefinition definition)
            actionId = catalog.GetOrAdd(definition);

        // 有招时相位无意义；空闲才把 Locomotion Clip 键带给幽灵，避免 T-Pose
        byte locomotionPhase = 0;
        if (!action.IsActive && actor.Animation != null && actor.Animation.CurrentKey.HasValue)
            locomotionPhase = (byte)actor.Animation.CurrentKey.Value;

        int healthMilli = actor.Numeric != null
            ? actor.Numeric.Attributes.GetCurrent(AttributeId.Health)
            : 0;

        return ReplicationSnapshotBuilder.FromAuthority(
            actor.SimulationId,
            actor.TeamId,
            kind,
            actor.MotorSim,
            in action,
            actionId,
            actor.TargetingSnapshot.SelectedTargetId,
            healthMilli,
            flagsPacked: 0,
            VitalityReplicationEdge.None,
            locomotionPhase: locomotionPhase);
    }
}

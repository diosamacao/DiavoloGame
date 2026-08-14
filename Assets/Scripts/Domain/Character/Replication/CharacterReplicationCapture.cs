using System;
using UnityEngine;

/// <summary>从权威 CharacterActor 组装下行快照；读 Vitality 边沿，不读 CameraLock / Look / Lean。</summary>
public static class CharacterReplicationCapture
{
    /// <summary>
    /// 用 Motor / Action / Numeric / 动画键填充最小复制集。
    /// actionId 写入共享 Catalog；空闲时复制 AnimationKey 与归一化时间。
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

        // 有招时相位无意义；空闲才把 Locomotion Key + 归一化时间带给幽灵
        byte locomotionPhase = 0;
        ushort locomotionNormalizedMilli = 0;
        if (!action.IsActive && actor.Animation != null)
        {
            if (actor.Animation.CurrentKey.HasValue)
                locomotionPhase = (byte)actor.Animation.CurrentKey.Value;
            locomotionNormalizedMilli = PackNormalizedTime(actor.Animation.NormalizedTime);
        }

        int healthMilli = actor.Numeric != null
            ? actor.Numeric.Attributes.GetCurrent(AttributeId.Health)
            : 0;

        // 把当帧 wish 单位方向写入 moveV*，与位姿同一 Tick 延迟，供幽灵黄箭对齐观察
        PackWishDirection(actor.DebugMoveWishWorldDirection, out int moveVxMm, out int moveVzMm);

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
            actor.Vitality != null
                ? actor.Vitality.ReplicationEdge
                : VitalityReplicationEdge.None,
            moveVxMm,
            moveVzMm,
            locomotionPhase: locomotionPhase,
            locomotionNormalizedMilli: locomotionNormalizedMilli);
    }

    /// <summary>水平 wish 写成约 1m/s 的毫米速度；无输入则为 0。</summary>
    static void PackWishDirection(Vector3 wishWorld, out int moveVxMm, out int moveVzMm)
    {
        wishWorld.y = 0f;
        if (wishWorld.sqrMagnitude < 0.0001f)
        {
            moveVxMm = 0;
            moveVzMm = 0;
            return;
        }

        Vector3 n = wishWorld.normalized;
        moveVxMm = MotionQuantization.MetersToMm(n.x);
        moveVzMm = MotionQuantization.MetersToMm(n.z);
    }

    /// <summary>归一化时间 ×1000，上限 ushort；循环片可大于 1000。</summary>
    static ushort PackNormalizedTime(float normalizedTime)
    {
        if (normalizedTime <= 0f)
            return 0;

        int milli = (int)Math.Round(normalizedTime * 1000.0);
        if (milli > ushort.MaxValue)
            return ushort.MaxValue;
        return (ushort)milli;
    }
}

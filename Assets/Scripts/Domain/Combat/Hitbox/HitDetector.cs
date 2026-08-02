using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 逻辑坐标命中检测：攻击/受击 OBB 由 MotorSim 根位姿构建；挂点仅提供相对根的局部 TRS。
/// </summary>
public static class HitDetector
{
    /// <summary>检测指定招式帧的全部 Hitbox，并把结果收集为稳定身份命中事件。</summary>
    public static void ProcessHitboxesAtFrame(
        ActionDefinition action,
        int frame,
        SimCombatPose attackerPose,
        int attackerTeamId,
        Func<HitboxNotifyState, Vector3> resolveAttachLocalPosition,
        Func<HitboxNotifyState, Quaternion> resolveAttachLocalRotation,
        HashSet<(int HitboxIndex, SimActorId TargetId)> hitPairs,
        IActionSimHitReceiver hitReceiver,
        IReadOnlyList<IHurtboxTarget> activeTargets,
        SimActorId attackerId,
        int actionInstanceId,
        CombatHitPipeline hitPipeline,
        Transform attackerRootForContext)
    {
        if (activeTargets == null
            || activeTargets.Count == 0
            || !attackerId.IsValid
            || hitPipeline == null)
        {
            return;
        }

        HitboxNotifyState[] hitboxes = action.HitboxStates;
        if (hitboxes == null || hitboxes.Length == 0)
            return;

        for (int hitboxIndex = 0; hitboxIndex < hitboxes.Length; hitboxIndex++)
        {
            HitboxNotifyState hitbox = hitboxes[hitboxIndex];
            if (hitbox == null || !hitbox.IsActiveAtFrame(frame))
                continue;

            Vector3 attachLocalPos = resolveAttachLocalPosition != null
                ? resolveAttachLocalPosition(hitbox)
                : Vector3.zero;
            Quaternion attachLocalRot = resolveAttachLocalRotation != null
                ? resolveAttachLocalRotation(hitbox)
                : Quaternion.identity;

            HitboxOrientedBox attackBox = HitboxMath.BuildFromHitboxLogical(
                in attackerPose,
                attachLocalPos,
                attachLocalRot,
                hitbox);

            foreach (IHurtboxTarget target in activeTargets)
            {
                if (target == null)
                    continue;

                if (!target.SimulationId.IsValid)
                    continue;

                // 自身：用稳定 Sim Id，禁止再比 Transform 层级
                if (target.SimulationId == attackerId)
                    continue;

                if (target is ITargetable targetable
                    && (!targetable.IsAlive || targetable.TeamId == attackerTeamId))
                {
                    continue;
                }

                var pair = (hitboxIndex, target.SimulationId);
                if (hitPairs.Contains(pair))
                    continue;

                if (!HitboxMath.Intersects(attackBox, target.GetLogicalHurtbox()))
                    continue;

                hitPairs.Add(pair);
                var context = new ActionHitContext(action, hitbox, attackerRootForContext, actionInstanceId);
                hitPipeline.Collect(
                    attackerId,
                    actionInstanceId,
                    hitboxIndex,
                    target,
                    hitReceiver,
                    in context);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 逻辑坐标命中检测：攻击/受击 OBB 相交；攻击盒由调用方解析（跟随挂点或世界冻结）。
/// </summary>
public static class HitDetector
{
    /// <summary>检测指定招式帧的全部 Hitbox，并把结果收集为稳定身份命中事件。</summary>
    public static void ProcessHitboxesAtFrame(
        ActionDefinition action,
        int frame,
        int attackerTeamId,
        Func<int, HitboxNotifyState, HitboxOrientedBox> resolveAttackBox,
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
            || hitPipeline == null
            || resolveAttackBox == null)
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

            HitboxOrientedBox attackBox = resolveAttackBox(hitboxIndex, hitbox);

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

                HitboxOrientedBox hurtbox = target.GetLogicalHurtbox();
                if (!HitboxMath.Intersects(attackBox, hurtbox))
                    continue;

                // 表现接触点：攻击盒中心投到受击盒（方案 A）；不回写 Sim
                Vector3 hitPoint = HitboxMath.EstimateContactPointOnHurtbox(in attackBox, in hurtbox);

                hitPairs.Add(pair);
                var context = new ActionHitContext(
                    action,
                    hitbox,
                    attackerRootForContext,
                    actionInstanceId,
                    attackerId);
                hitPipeline.Collect(
                    attackerId,
                    actionInstanceId,
                    hitboxIndex,
                    target,
                    hitReceiver,
                    in context,
                    hitPoint);
            }
        }
    }

    /// <summary>
    /// 客机预测卡肉：只对 UseHitStop 的盒做几何重叠并 RequestHitStop。
    /// 不 Collect、不 OnHit、不写 Numeric。
    /// </summary>
    public static void ApplyPredictedHitStopAtFrame(
        ActionDefinition action,
        int frame,
        int attackerTeamId,
        Func<int, HitboxNotifyState, HitboxOrientedBox> resolveAttackBox,
        HashSet<(int HitboxIndex, SimActorId TargetId)> hitPairs,
        IActionSimHitReceiver hitReceiver,
        IReadOnlyList<IHurtboxTarget> activeTargets,
        SimActorId attackerId,
        int actionInstanceId)
    {
        if (activeTargets == null
            || activeTargets.Count == 0
            || !attackerId.IsValid
            || hitReceiver == null
            || resolveAttackBox == null
            || action == null)
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

            HitFeedbackSettings feedback = hitbox.Payload.Feedback;
            if (feedback == null || !feedback.UseHitStop)
                continue;

            HitboxOrientedBox attackBox = resolveAttackBox(hitboxIndex, hitbox);

            foreach (IHurtboxTarget target in activeTargets)
            {
                if (target == null || !target.SimulationId.IsValid)
                    continue;

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

                HitboxOrientedBox hurtbox = target.GetLogicalHurtbox();
                if (!HitboxMath.Intersects(attackBox, hurtbox))
                    continue;

                hitPairs.Add(pair);
                hitReceiver.RequestHitStop(
                    actionInstanceId,
                    feedback.HitStopFrames,
                    feedback.HitStopOncePerAction);
            }
        }
    }
}

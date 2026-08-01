using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Transform 命中检测临时入口；只写帧末流水线，L2 将删除该非纯模拟几何路径。</summary>
public static class HitDetector
{
    /// <summary>检测指定招式帧的全部 Hitbox，并把结果收集为稳定身份命中事件。</summary>
    public static void ProcessHitboxesAtFrame(
        ActionDefinition action,
        int frame,
        Transform root,
        int attackerTeamId,
        Func<HitboxNotifyState, Transform> resolveAnchor,
        HashSet<(int HitboxIndex, SimActorId TargetId)> hitPairs,
        IActionSimHitReceiver hitReceiver,
        IReadOnlyList<IHurtboxTarget> activeTargets,
        SimActorId attackerId,
        int actionInstanceId,
        CombatHitPipeline hitPipeline)
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

            Transform anchor = resolveAnchor != null ? resolveAnchor(hitbox) : root;
            HitboxOrientedBox attackBox = HitboxMath.BuildFromHitbox(root, anchor, hitbox);
            foreach (IHurtboxTarget target in activeTargets)
            {
                if (target == null)
                    continue;

                // 非 SimulationWorld 目标没有跨端稳定身份，不能进入权威命中事件。
                if (!target.SimulationId.IsValid)
                    continue;

                // 模型子层级可能残留 HurtboxTarget；整棵角色层级都视为自身，禁止生成命中事件。
                if (IsSameHierarchy(root, target.TargetTransform))
                {
                    continue;
                }

                if (target is ITargetable targetable
                    && (!targetable.IsAlive || targetable.TeamId == attackerTeamId))
                {
                    continue;
                }

                // 数组下标代表时间轴中的 Hitbox 窗口实例；显示 Id 重复也必须各自结算一次。
                var pair = (hitboxIndex, target.SimulationId);
                if (hitPairs.Contains(pair))
                    continue;

                if (!HitboxMath.Intersects(attackBox, target.GetWorldHurtbox()))
                    continue;

                hitPairs.Add(pair);
                var context = new ActionHitContext(action, hitbox, root, actionInstanceId);
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

    /// <summary>判断两个 Transform 是否属于同一角色层级。</summary>
    static bool IsSameHierarchy(Transform root, Transform target)
    {
        if (root == null || target == null)
            return false;

        return target == root
            || target.IsChildOf(root)
            || root.IsChildOf(target);
    }
}

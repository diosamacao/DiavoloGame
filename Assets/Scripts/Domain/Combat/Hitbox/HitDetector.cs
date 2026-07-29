using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>命中检测批处理入口；只接收目标集合与命中回调，不依赖架构层。</summary>
public static class HitDetector
{
    /// <summary>检测指定招式帧的全部 Hitbox，并把命中结果交给调用方处理。</summary>
    public static void ProcessHitboxesAtFrame(
        ActionDefinition action,
        int frame,
        Transform root,
        int attackerTeamId,
        Func<HitboxNotifyState, Transform> resolveAnchor,
        HashSet<(string HitboxId, int TargetId)> hitPairs,
        IActionHitReceiver hitReceiver,
        IReadOnlyList<IHurtboxTarget> activeTargets,
        Action<ActionHitContext, IHurtboxTarget, IActionHitReceiver, Transform> hitDetected)
    {
        if (activeTargets == null || activeTargets.Count == 0 || hitDetected == null)
            return;

        IReadOnlyList<HitboxNotifyState> activeHitboxes = action.GetActiveHitboxesAtFrame(frame);
        if (activeHitboxes.Count == 0)
            return;

        foreach (HitboxNotifyState hitbox in activeHitboxes)
        {
            Transform anchor = resolveAnchor != null ? resolveAnchor(hitbox) : root;
            HitboxOrientedBox attackBox = HitboxMath.BuildFromHitbox(root, anchor, hitbox);
            foreach (IHurtboxTarget target in activeTargets)
            {
                if (target == null)
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

                var pair = (hitbox.HitboxId, target.TargetInstanceId);
                if (hitPairs.Contains(pair))
                    continue;

                if (!HitboxMath.Intersects(attackBox, target.GetWorldHurtbox()))
                    continue;

                hitPairs.Add(pair);
                var context = new ActionHitContext(action, hitbox, root);
                hitDetected.Invoke(context, target, hitReceiver, target.TargetTransform);
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

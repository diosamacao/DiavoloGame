using System.Collections.Generic;
using UnityEngine;

/// <summary>命中检测批处理入口；短期仍线性扫描 TargetRegistry，后续可替换为空间分区。</summary>
public static class HitDetectionSystem
{
    /// <summary>检测指定招式帧的全部 Hitbox，并把命中结果回流给目标、攻击者和反馈系统。</summary>
    public static void ProcessHitboxesAtFrame(
        ActionDefinition action,
        int frame,
        Transform root,
        Transform anchor,
        HashSet<(string HitboxId, int TargetId)> hitPairs,
        IActionHitReceiver hitReceiver)
    {
        IReadOnlyList<HitboxKeyframe> activeHitboxes = action.GetActiveHitboxesAtFrame(frame);
        if (activeHitboxes.Count == 0)
            return;

        foreach (HitboxKeyframe hitbox in activeHitboxes)
        {
            HitboxOrientedBox attackBox = HitboxMath.BuildFromHitbox(root, anchor, hitbox);
            foreach (IHurtboxTarget target in TargetRegistry.ActiveTargets)
            {
                if (target == null)
                    continue;

                var pair = (hitbox.HitboxId, target.TargetInstanceId);
                if (hitPairs.Contains(pair))
                    continue;

                if (!HitboxMath.Intersects(attackBox, target.GetWorldHurtbox()))
                    continue;

                hitPairs.Add(pair);
                var context = new ActionHitContext(action, hitbox, root);
                target.OnHit(in context);
                hitReceiver?.NotifyHit(in context);

                Transform targetTransform = (target as Component)?.transform;
                CombatHitFeedback.OnAttackHit(in context, targetTransform);
            }
        }
    }
}

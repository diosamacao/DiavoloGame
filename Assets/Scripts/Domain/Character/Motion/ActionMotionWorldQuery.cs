using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 从当前活跃 Hurtbox 目标表解析逻辑 Pose（中心作位置；朝向暂 0，Adhesion 只用 XZ）。
/// </summary>
public sealed class ActionMotionWorldQuery : IActionMotionWorldQuery
{
    readonly Func<IReadOnlyList<IHurtboxTarget>> _targetsProvider;

    /// <summary>绑定与 Hitbox 相同的活跃目标列表提供者。</summary>
    public ActionMotionWorldQuery(Func<IReadOnlyList<IHurtboxTarget>> targetsProvider)
    {
        _targetsProvider = targetsProvider;
    }

    /// <inheritdoc />
    public bool TryGetCommittedCombatPose(SimActorId actorId, out SimCombatPose pose)
    {
        pose = default;
        if (!actorId.IsValid || _targetsProvider == null)
            return false;

        IReadOnlyList<IHurtboxTarget> targets = _targetsProvider.Invoke();
        if (targets == null)
            return false;

        for (int i = 0; i < targets.Count; i++)
        {
            IHurtboxTarget target = targets[i];
            if (target == null || target.SimulationId != actorId)
                continue;

            HitboxOrientedBox hurt = target.GetLogicalHurtbox();
            // Adhesion 仅用水平位置；yaw 对连线公式无影响
            pose = new SimCombatPose(hurt.Center, 0f);
            return true;
        }

        return false;
    }
}

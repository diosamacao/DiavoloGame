using System;
using System.Collections.Generic;

/// <summary>
/// 从当前活跃 Hurtbox 目标表解析逻辑根 Pose（含朝向，供 RelocateBehind）。
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

            pose = target.GetLogicalCombatPose();
            return true;
        }

        return false;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>索敌解析入口；只基于调用方提供的候选目标集合做无副作用计算。</summary>
public static class TargetingResolver
{
    /// <summary>按设置为攻击者选择锁定目标。</summary>
    public static ITargetable Select(
        IReadOnlyList<IHurtboxTarget> activeTargets,
        Vector3 origin,
        Vector3 forward,
        int attackerTeamId,
        Transform attackerRoot,
        in TargetLockSettings settings)
    {
        return TargetSelector.Select(activeTargets, origin, forward, attackerTeamId, attackerRoot, in settings);
    }

    /// <summary>计算指向目标的水平方向。</summary>
    public static bool TryGetDirectionToTarget(Vector3 origin, ITargetable target, out Vector3 direction)
    {
        return TargetSelector.TryGetDirectionToTarget(origin, target, out direction);
    }
}

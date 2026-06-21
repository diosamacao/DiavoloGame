using UnityEngine;

/// <summary>索敌批处理入口；当前复用 TargetSelector，后续可在这里集中缓存本帧查询。</summary>
public static class TargetingSystem
{
    /// <summary>按设置为攻击者选择锁定目标。</summary>
    public static ITargetable Select(
        Vector3 origin,
        Vector3 forward,
        int attackerTeamId,
        Transform attackerRoot,
        in TargetLockSettings settings)
    {
        return TargetSelector.Select(origin, forward, attackerTeamId, attackerRoot, in settings);
    }

    /// <summary>计算指向目标的水平方向。</summary>
    public static bool TryGetDirectionToTarget(Vector3 origin, ITargetable target, out Vector3 direction)
    {
        return TargetSelector.TryGetDirectionToTarget(origin, target, out direction);
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>从候选目标集合中按策略选取单个 ITargetable。</summary>
public static class TargetSelector
{
    /// <summary>按 settings 与攻击者状态选取最佳目标；无候选时返回 null。</summary>
    public static ITargetable Select(
        IReadOnlyList<IHurtboxTarget> activeTargets,
        Vector3 origin,
        Vector3 forward,
        int attackerTeamId,
        Transform attackerRoot,
        in TargetLockSettings settings)
    {
        if (!settings.Enabled || settings.LockRange <= 0f)
            return null;

        forward = Flatten(forward);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        ITargetable best = null;
        float bestDistanceSq = float.MaxValue;
        float bestHealth = float.MaxValue;

        if (activeTargets == null || activeTargets.Count == 0)
            return null;

        foreach (IHurtboxTarget candidate in activeTargets)
        {
            if (candidate is not ITargetable target)
                continue;

            if (!IsEligible(target, origin, forward, attackerTeamId, attackerRoot, settings, out float distanceSq))
                continue;

            switch (settings.Policy)
            {
                case TargetSelectionPolicy.LowestHealth:
                    if (TryBeatByHealth(target, distanceSq, best, bestHealth, bestDistanceSq))
                    {
                        best = target;
                        bestHealth = target.CurrentHealth;
                        bestDistanceSq = distanceSq;
                    }

                    break;

                default:
                    if (distanceSq < bestDistanceSq)
                    {
                        best = target;
                        bestDistanceSq = distanceSq;
                        bestHealth = target.CurrentHealth;
                    }

                    break;
            }
        }

        return best;
    }

    /// <summary>计算从 origin 指向锁定瞄准点的水平方向。</summary>
    public static bool TryGetDirectionToTarget(Vector3 origin, ITargetable target, out Vector3 direction)
    {
        direction = Vector3.zero;

        if (target?.AimTransform == null)
            return false;

        direction = Flatten(target.AimTransform.position - origin);
        if (direction.sqrMagnitude < 0.0001f)
            return false;

        direction.Normalize();
        return true;
    }

    static bool IsEligible(
        ITargetable target,
        Vector3 origin,
        Vector3 forward,
        int attackerTeamId,
        Transform attackerRoot,
        in TargetLockSettings settings,
        out float distanceSq)
    {
        distanceSq = float.MaxValue;

        if (target == null || !target.IsAlive)
            return false;

        if (target.TeamId == attackerTeamId)
            return false;

        if (attackerRoot != null && IsSameHierarchy(attackerRoot, target.AimTransform))
            return false;

        if (target.AimTransform == null)
            return false;

        Vector3 toTarget = Flatten(target.AimTransform.position - origin);
        distanceSq = toTarget.sqrMagnitude;

        float maxRangeSq = settings.LockRange * settings.LockRange;
        if (distanceSq > maxRangeSq || distanceSq < 0.0001f)
            return false;

        if (settings.UsesForwardConeFilter)
        {
            toTarget.Normalize();
            float halfAngle = settings.ForwardConeAngle * 0.5f;
            if (Vector3.Angle(forward, toTarget) > halfAngle)
                return false;
        }

        return true;
    }

    static bool TryBeatByHealth(
        ITargetable candidate,
        float candidateDistanceSq,
        ITargetable currentBest,
        float currentBestHealth,
        float currentBestDistanceSq)
    {
        float candidateHealth = candidate.CurrentHealth;

        if (currentBest == null)
            return true;

        if (candidateHealth < currentBestHealth - 0.001f)
            return true;

        if (Mathf.Abs(candidateHealth - currentBestHealth) <= 0.001f)
            return candidateDistanceSq < currentBestDistanceSq;

        return false;
    }

    static bool IsSameHierarchy(Transform attackerRoot, Transform targetTransform)
    {
        if (attackerRoot == null || targetTransform == null)
            return false;

        return targetTransform == attackerRoot
            || targetTransform.IsChildOf(attackerRoot)
            || attackerRoot.IsChildOf(targetTransform);
    }

    static Vector3 Flatten(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }
}

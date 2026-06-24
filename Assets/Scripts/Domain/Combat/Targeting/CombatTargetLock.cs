using UnityEngine;

/// <summary>攻击侧索敌运行时：起手 Acquire、帧间 Validate，并向 ActionRotationDriver 提供锁定方向。</summary>
public sealed class CombatTargetLock
{
    readonly Transform attackerRoot;
    readonly int attackerTeamId;
    readonly Transform aimOrigin;
    ITargetable _lockedTarget;
    TargetLockSettings _activeSettings;
    ActionDefinition _lockedForAction;

    /// <summary>当前是否存在通过 Validate 的有效锁定。</summary>
    public bool HasValidLock =>
        _lockedTarget != null
        && _activeSettings != null
        && IsTargetStillValid(_lockedTarget, _activeSettings);

    /// <summary>当前锁定的可瞄准目标（可能已失效，使用前请检查 HasValidLock）。</summary>
    public ITargetable LockedTarget => _lockedTarget;

    Transform Origin => aimOrigin != null ? aimOrigin : attackerRoot;

    /// <summary>创建索敌锁定状态；不挂载到玩家对象。</summary>
    public CombatTargetLock(Transform attacker, int team, Transform origin)
    {
        attackerRoot = attacker;
        attackerTeamId = team;
        aimOrigin = origin;
    }

    /// <summary>每帧由 ActionRotationDriver 在 Action 状态下调用，按 ActionSession 处理 Acquire / Validate。</summary>
    public void Tick(ActionSession session)
    {
        if (session == null || !session.IsActive)
        {
            ClearLock();
            return;
        }

        ActionDefinition action = session.CurrentAction;
        if (_lockedForAction != action)
            TryAcquireForAction(action);

        if (_lockedTarget != null && !IsTargetStillValid(_lockedTarget, _activeSettings))
            ClearLock();
    }

    /// <summary>离开 Action 或招式结束时清空锁定。</summary>
    public void ClearLock()
    {
        _lockedTarget = null;
        _lockedForAction = null;
        _activeSettings = default;
    }

    /// <summary>返回指向当前锁定目标的水平方向；无有效锁时返回 false。</summary>
    public bool TryGetLockDirection(out Vector3 direction)
    {
        direction = Vector3.zero;

        if (!HasValidLock)
            return false;

        return TargetingSystem.TryGetDirectionToTarget(Origin.position, _lockedTarget, out direction);
    }

    void TryAcquireForAction(ActionDefinition action)
    {
        _lockedForAction = action;
        _lockedTarget = null;
        _activeSettings = default;

        if (action == null || !action.HasTargetLock)
            return;

        TargetLockSettings settings = action.TargetLockSettings;
        _activeSettings = settings;
        _lockedTarget = TargetingSystem.Select(
            Origin.position,
            attackerRoot.forward,
            attackerTeamId,
            attackerRoot,
            in settings);
    }

    bool IsTargetStillValid(ITargetable target, in TargetLockSettings settings)
    {
        if (target == null || settings == null || !settings.Enabled || !target.IsAlive)
            return false;

        if (target.TeamId == attackerTeamId)
            return false;

        if (target.AimTransform == null)
            return false;

        if (IsSameHierarchy(attackerRoot, target.AimTransform))
            return false;

        Vector3 toTarget = target.AimTransform.position - Origin.position;
        toTarget.y = 0f;

        float maxRangeSq = settings.LockRange * settings.LockRange;
        if (toTarget.sqrMagnitude > maxRangeSq || toTarget.sqrMagnitude < 0.0001f)
            return false;

        if (settings.UsesForwardConeFilter)
        {
            Vector3 forward = attackerRoot.forward;
            forward.y = 0f;
            forward.Normalize();

            toTarget.Normalize();
            float halfAngle = settings.ForwardConeAngle * 0.5f;
            if (Vector3.Angle(forward, toTarget) > halfAngle)
                return false;
        }

        return true;
    }

    static bool IsSameHierarchy(Transform attackerRoot, Transform targetTransform)
    {
        if (attackerRoot == null || targetTransform == null)
            return false;

        return targetTransform == attackerRoot
            || targetTransform.IsChildOf(attackerRoot)
            || attackerRoot.IsChildOf(targetTransform);
    }
}

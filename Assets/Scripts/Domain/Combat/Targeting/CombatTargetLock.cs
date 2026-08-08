using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>攻击侧索敌运行时：起手 Acquire、帧间 Validate，并向 ActionRotationDriver 提供锁定方向。</summary>
public sealed class CombatTargetLock
{
    readonly Transform attackerRoot;
    readonly int attackerTeamId;
    readonly Transform aimOrigin;
    readonly Func<IReadOnlyList<IHurtboxTarget>> activeTargetsProvider;
    ITargetable _lockedTarget;
    TargetLockSettings _activeSettings;
    ActionGraph _lockedForGraph;
    string _lockedForNodeId;

    /// <summary>当前是否存在通过 Validate 的有效锁定。</summary>
    public bool HasValidLock =>
        _lockedTarget != null
        && _activeSettings != null
        && IsTargetStillValid(_lockedTarget, _activeSettings);

    /// <summary>当前锁定的可瞄准目标（可能已失效，使用前请检查 HasValidLock）。</summary>
    public ITargetable LockedTarget => _lockedTarget;

    Transform Origin => aimOrigin != null ? aimOrigin : attackerRoot;

    /// <summary>创建索敌锁定状态；不挂载到玩家对象。</summary>
    public CombatTargetLock(
        Transform attacker,
        int team,
        Transform origin,
        Func<IReadOnlyList<IHurtboxTarget>> targetsProvider)
    {
        attackerRoot = attacker;
        attackerTeamId = team;
        aimOrigin = origin;
        activeTargetsProvider = targetsProvider;
    }

    /// <summary>每帧由 ActionRotationDriver 按纯模拟快照处理 Acquire / Validate。</summary>
    public void Tick(in ActionSimSnapshot snapshot)
    {
        if (!snapshot.IsActive)
        {
            ClearLock();
            return;
        }

        ActionGraph graph = snapshot.Graph as ActionGraph;
        if (graph == null || !graph.TryGetNode(snapshot.NodeId, out ActionGraphNode node))
        {
            ClearLock();
            return;
        }

        if (_lockedForGraph != graph || _lockedForNodeId != node.NodeId)
            TryAcquireForNode(graph, node);

        if (_lockedTarget != null && !IsTargetStillValid(_lockedTarget, _activeSettings))
            ClearLock();
    }

    /// <summary>离开 Action 或招式结束时清空锁定。</summary>
    public void ClearLock()
    {
        _lockedTarget = null;
        _lockedForGraph = null;
        _lockedForNodeId = null;
        _activeSettings = default;
    }

    /// <summary>
    /// 动作起手立即按图节点索敌（早于 ActionRotation.Tick），供固化 ActionTargetId。
    /// </summary>
    public void AcquireForActionNode(ActionGraph graph, string nodeId)
    {
        if (graph == null || !graph.TryGetNode(nodeId, out ActionGraphNode node))
        {
            ClearLock();
            return;
        }

        TryAcquireForNode(graph, node);
    }

    /// <summary>当前有效锁的 SimulationId；无效时返回 Invalid。</summary>
    public SimActorId ResolveLockedSimulationId() =>
        HasValidLock && LockedTarget != null
            ? LockedTarget.SimulationId
            : SimActorId.Invalid;

    /// <summary>返回指向当前锁定目标的水平方向；无有效锁时返回 false。</summary>
    public bool TryGetLockDirection(out Vector3 direction)
    {
        direction = Vector3.zero;

        if (!HasValidLock)
            return false;

        return TargetingResolver.TryGetDirectionToTarget(Origin.position, _lockedTarget, out direction);
    }

    /// <summary>按当前节点索敌配置解析锁定转向平滑时间。</summary>
    public float ResolveLockSmoothTime(float rotationWindowSmoothTime) =>
        _activeSettings != null
            ? _activeSettings.ResolveLockSmoothTime(rotationWindowSmoothTime)
            : rotationWindowSmoothTime;

    /// <summary>进入新图节点时按节点策略重新获取目标。</summary>
    void TryAcquireForNode(ActionGraph graph, ActionGraphNode node)
    {
        _lockedForGraph = graph;
        _lockedForNodeId = node?.NodeId;
        _lockedTarget = null;
        _activeSettings = default;

        if (node == null || !node.HasTargetLock)
            return;

        TargetLockSettings settings = node.TargetLockSettings;
        _activeSettings = settings;
        _lockedTarget = TargetingResolver.Select(
            activeTargetsProvider?.Invoke(),
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

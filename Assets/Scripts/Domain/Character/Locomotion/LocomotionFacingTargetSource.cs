using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FaceTarget 朝向源：优先动作索敌锁，其次软锁最近敌对（半径由 Profile 提供）。
/// </summary>
public sealed class LocomotionFacingTargetSource : ILocomotionFacingTargetSource
{
    readonly Transform _self;
    readonly int _teamId;
    readonly CombatTargetLock _actionLock;
    readonly Func<IReadOnlyList<IHurtboxTarget>> _targetsProvider;
    readonly Func<float> _softFocusRadiusMeters;

    /// <summary>装配索敌锁 + 可选软锁半径查询。</summary>
    public LocomotionFacingTargetSource(
        Transform self,
        int teamId,
        CombatTargetLock actionLock,
        Func<IReadOnlyList<IHurtboxTarget>> targetsProvider,
        Func<float> softFocusRadiusMeters)
    {
        _self = self;
        _teamId = teamId;
        _actionLock = actionLock;
        _targetsProvider = targetsProvider;
        _softFocusRadiusMeters = softFocusRadiusMeters ?? (() => 0f);
    }

    /// <inheritdoc />
    public bool TryGetFacingWorldDirection(out Vector3 planarForward)
    {
        if (_actionLock != null && _actionLock.TryGetLockDirection(out planarForward))
            return true;

        return TrySoftFocusNearest(out planarForward);
    }

    bool TrySoftFocusNearest(out Vector3 planarForward)
    {
        planarForward = Vector3.zero;
        float radius = _softFocusRadiusMeters != null ? _softFocusRadiusMeters() : 0f;
        if (radius <= 0.01f || _self == null || _targetsProvider == null)
            return false;

        IReadOnlyList<IHurtboxTarget> targets = _targetsProvider.Invoke();
        if (targets == null || targets.Count == 0)
            return false;

        Vector3 origin = _self.position;
        float bestSq = radius * radius;
        Vector3 bestDir = Vector3.zero;
        bool found = false;

        for (int i = 0; i < targets.Count; i++)
        {
            // 软锁只认可索敌目标（ITargetable）；纯 Hurtbox 无阵营/瞄准点
            if (targets[i] is not ITargetable target)
                continue;
            if (!target.IsAlive || target.TeamId == _teamId)
                continue;

            Transform aim = target.AimTransform;
            if (aim == null)
                continue;

            Vector3 delta = aim.position - origin;
            delta.y = 0f;
            float sq = delta.sqrMagnitude;
            if (sq < 0.0001f || sq > bestSq)
                continue;

            bestSq = sq;
            bestDir = delta;
            found = true;
        }

        if (!found)
            return false;

        planarForward = bestDir.normalized;
        return true;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>敌人每帧感知快照；决策层只读取该值，不直接查找场景对象。</summary>
public readonly struct EnemyPerceptionSnapshot
{
    /// <summary>创建目标与自身状态快照。</summary>
    public EnemyPerceptionSnapshot(
        bool hasTarget,
        Vector3 targetPosition,
        Vector3 planarDirection,
        float planarDistance,
        CharacterStateType characterState,
        bool isDead)
    {
        HasTarget = hasTarget;
        TargetPosition = targetPosition;
        PlanarDirection = planarDirection;
        PlanarDistance = planarDistance;
        CharacterState = characterState;
        IsDead = isDead;
    }

    public bool HasTarget { get; }
    public Vector3 TargetPosition { get; }
    public Vector3 PlanarDirection { get; }
    public float PlanarDistance { get; }
    public CharacterStateType CharacterState { get; }
    public bool IsDead { get; }
}

/// <summary>
/// 敌人感知服务。从注入的玩家根列表中取水平最近者，不写死唯一玩家 Transform。
/// </summary>
public sealed class EnemyPerception
{
    readonly Transform _self;
    readonly Func<IReadOnlyList<Transform>> _playerRootsProvider;
    readonly Func<CharacterStateType> _stateProvider;
    readonly Func<bool> _isDeadProvider;

    /// <summary>创建敌人感知服务；playerRootsProvider 每帧提供候选玩家权威根。</summary>
    public EnemyPerception(
        Transform self,
        Func<IReadOnlyList<Transform>> playerRootsProvider,
        Func<CharacterStateType> stateProvider,
        Func<bool> isDeadProvider)
    {
        _self = self;
        _playerRootsProvider = playerRootsProvider;
        _stateProvider = stateProvider;
        _isDeadProvider = isDeadProvider;
    }

    /// <summary>在玩家列表中选水平最近目标，采样距离、方向和自身状态。</summary>
    public EnemyPerceptionSnapshot Capture()
    {
        Transform target = SelectClosestPlayerRoot();
        bool hasTarget = _self != null && target != null;
        Vector3 targetPosition = hasTarget ? target.position : Vector3.zero;
        Vector3 direction = hasTarget ? targetPosition - _self.position : Vector3.zero;
        direction.y = 0f;
        float distance = direction.magnitude;
        if (distance > 0.0001f)
            direction /= distance;

        return new EnemyPerceptionSnapshot(
            hasTarget,
            targetPosition,
            direction,
            distance,
            _stateProvider != null ? _stateProvider() : CharacterStateType.Locomotion,
            _isDeadProvider != null && _isDeadProvider());
    }

    /// <summary>忽略 Y，取列表中距离自身最近的有效 Transform。</summary>
    Transform SelectClosestPlayerRoot()
    {
        if (_self == null || _playerRootsProvider == null)
            return null;

        IReadOnlyList<Transform> roots = _playerRootsProvider.Invoke();
        if (roots == null || roots.Count == 0)
            return null;

        Vector3 selfPos = _self.position;
        Transform best = null;
        float bestDistSq = float.MaxValue;
        for (int i = 0; i < roots.Count; i++)
        {
            Transform root = roots[i];
            if (root == null)
                continue;

            Vector3 delta = root.position - selfPos;
            delta.y = 0f;
            float distSq = delta.sqrMagnitude;
            if (distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            best = root;
        }

        return best;
    }
}

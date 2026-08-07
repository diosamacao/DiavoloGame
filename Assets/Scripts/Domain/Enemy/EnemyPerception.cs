using System;
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

/// <summary>敌人感知服务；通过注入目标提供器生成无副作用快照。</summary>
public sealed class EnemyPerception
{
    readonly Transform _self;
    readonly Func<Transform> _targetProvider;
    readonly Func<CharacterStateType> _stateProvider;
    readonly Func<bool> _isDeadProvider;

    /// <summary>创建敌人感知服务。</summary>
    public EnemyPerception(
        Transform self,
        Func<Transform> targetProvider,
        Func<CharacterStateType> stateProvider,
        Func<bool> isDeadProvider)
    {
        _self = self;
        _targetProvider = targetProvider;
        _stateProvider = stateProvider;
        _isDeadProvider = isDeadProvider;
    }

    /// <summary>采样当前目标距离、方向和角色状态。</summary>
    public EnemyPerceptionSnapshot Capture()
    {
        Transform target = _targetProvider?.Invoke();
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
}

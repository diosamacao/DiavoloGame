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

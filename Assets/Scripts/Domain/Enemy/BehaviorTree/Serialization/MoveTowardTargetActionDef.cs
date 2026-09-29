using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>MoveTowardTarget 行动定义；幅度/停步在节点上。</summary>
[Serializable]
public sealed class MoveTowardTargetActionDef : EnemyBehaviorNodeDef
{
    [SerializeField, Range(0f, 1f)] float magnitude = 1f;
    [SerializeField] float stopDistance = 1.2f;
    [SerializeField] bool faceTarget = true;

    /// <summary>本地前进轴幅度。</summary>
    public float Magnitude
    {
        get => magnitude;
        set => magnitude = Mathf.Clamp01(value);
    }

    /// <summary>贴身停步距离（米）。</summary>
    public float StopDistance
    {
        get => stopDistance;
        set => stopDistance = Mathf.Max(0f, value);
    }

    /// <summary>追击时是否请求面向目标。</summary>
    public bool FaceTarget
    {
        get => faceTarget;
        set => faceTarget = value;
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new MoveTowardTargetAction(magnitude, stopDistance, faceTarget));
}

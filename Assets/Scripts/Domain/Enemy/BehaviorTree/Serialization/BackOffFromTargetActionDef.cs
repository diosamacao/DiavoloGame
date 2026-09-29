using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>BackOffFromTarget 行动定义。</summary>
[Serializable]
public sealed class BackOffFromTargetActionDef : EnemyBehaviorNodeDef
{
    [SerializeField, Range(0f, 1f)] float magnitude = 1f;

    /// <summary>后退幅度。</summary>
    public float Magnitude
    {
        get => magnitude;
        set => magnitude = Mathf.Clamp01(value);
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new BackOffFromTargetAction(magnitude));
}

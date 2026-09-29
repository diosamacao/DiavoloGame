using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>StrafeAroundTarget 行动定义。</summary>
[Serializable]
public sealed class StrafeAroundTargetActionDef : EnemyBehaviorNodeDef
{
    [SerializeField] float sideSign = 1f;
    [SerializeField, Range(0f, 1f)] float magnitude = 0.35f;

    /// <summary>侧移符号：&gt;0 右，&lt;0 左。</summary>
    public float SideSign
    {
        get => sideSign;
        set => sideSign = value >= 0f ? 1f : -1f;
    }

    /// <summary>侧移幅度（宜小于 RunThreshold）。</summary>
    public float Magnitude
    {
        get => magnitude;
        set => magnitude = Mathf.Clamp01(value);
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new StrafeAroundTargetAction(sideSign, magnitude));
}

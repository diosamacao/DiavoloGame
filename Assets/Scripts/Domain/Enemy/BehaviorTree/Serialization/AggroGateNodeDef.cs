using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>AggroGate 定义：维护 IsAggroed 滞回后 Tick 子树。</summary>
[Serializable]
public sealed class AggroGateNodeDef : EnemyBehaviorNodeDef
{
    [SerializeField] float enterRadius = 10f;
    [SerializeField] float exitRadius = 14f;
    [SerializeReference] public EnemyBehaviorNodeDef child;

    /// <summary>进入仇恨的水平距离。</summary>
    public float EnterRadius
    {
        get => enterRadius;
        set => enterRadius = Mathf.Max(0f, value);
    }

    /// <summary>脱离仇恨的水平距离（至少等于 enter）。</summary>
    public float ExitRadius
    {
        get => exitRadius;
        set => exitRadius = Mathf.Max(0f, value);
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new AggroGateNode(
            enterRadius,
            exitRadius,
            child != null ? child.Build() : new StopMoveAction()));
}

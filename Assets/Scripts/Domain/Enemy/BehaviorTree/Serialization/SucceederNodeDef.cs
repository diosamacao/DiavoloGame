using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Succeeder 定义。</summary>
[Serializable]
public sealed class SucceederNodeDef : EnemyBehaviorNodeDef
{
    [SerializeReference] public EnemyBehaviorNodeDef child;

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new SucceederNode(child != null ? child.Build() : new StopMoveAction()));
}

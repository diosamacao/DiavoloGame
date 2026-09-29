using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Inverter 定义。</summary>
[Serializable]
public sealed class InverterNodeDef : EnemyBehaviorNodeDef
{
    [SerializeReference] public EnemyBehaviorNodeDef child;

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new InverterNode(child != null ? child.Build() : new StopMoveAction()));
}

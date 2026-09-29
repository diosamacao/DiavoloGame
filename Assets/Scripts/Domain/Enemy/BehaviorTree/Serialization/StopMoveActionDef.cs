using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>StopMove 行动定义。</summary>
[Serializable]
public sealed class StopMoveActionDef : EnemyBehaviorNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new StopMoveAction());
}

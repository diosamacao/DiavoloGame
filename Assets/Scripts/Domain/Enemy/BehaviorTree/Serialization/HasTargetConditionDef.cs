using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>HasTarget 条件装饰定义。</summary>
[Serializable]
public sealed class HasTargetConditionDef : EnemyBehaviorConditionNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new HasTargetCondition(BuildChild()));
}

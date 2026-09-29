using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>InCombatAggro 条件装饰定义。</summary>
[Serializable]
public sealed class InCombatAggroConditionDef : EnemyBehaviorConditionNodeDef
{
    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new InCombatAggroCondition(BuildChild()));
}

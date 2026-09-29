using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>CooldownReady 条件装饰定义。</summary>
[Serializable]
public sealed class CooldownReadyConditionDef : EnemyBehaviorConditionNodeDef
{
    [SerializeField] string cooldownId = EnemyCooldownIds.BasicAttack;

    /// <summary>冷却 id。</summary>
    public string CooldownId
    {
        get => cooldownId;
        set => cooldownId = value;
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new CooldownReadyCondition(cooldownId, BuildChild()));
}

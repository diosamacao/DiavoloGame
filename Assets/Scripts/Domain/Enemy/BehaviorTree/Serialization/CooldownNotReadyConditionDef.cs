using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>CooldownNotReady：冷却或失败重试占用中时放行（对峙支）。</summary>
[Serializable]
public sealed class CooldownNotReadyConditionDef : EnemyBehaviorConditionNodeDef
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
        Wrap(new CooldownNotReadyCondition(cooldownId, BuildChild()));
}

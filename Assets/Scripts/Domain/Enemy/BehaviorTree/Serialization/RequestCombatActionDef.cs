using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>RequestCombatAction 行动定义；Entry NodeId 须为 ActiveGraph Entry。</summary>
[Serializable]
public sealed class RequestCombatActionDef : EnemyBehaviorNodeDef
{
    [SerializeField] string entryNodeId = string.Empty;

    /// <summary>ActionGraph Entry 的 NodeId。</summary>
    public string EntryNodeId
    {
        get => entryNodeId;
        set => entryNodeId = value ?? string.Empty;
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new RequestCombatAction(entryNodeId));
}

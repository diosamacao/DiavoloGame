using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Sequence 定义。</summary>
[Serializable]
public sealed class SequenceNodeDef : EnemyBehaviorNodeDef
{
    [SerializeReference] public List<EnemyBehaviorNodeDef> children = new List<EnemyBehaviorNodeDef>();

    /// <inheritdoc />
    public override IBehaviorNode Build()
    {
        var built = new IBehaviorNode[children != null ? children.Count : 0];
        for (int i = 0; i < built.Length; i++)
            built[i] = children[i] != null ? children[i].Build() : new StopMoveAction();
        return Wrap(new SequenceNode(built));
    }
}

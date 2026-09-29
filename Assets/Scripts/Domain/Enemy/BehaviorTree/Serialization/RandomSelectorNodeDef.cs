using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>RandomSelector 定义；weights 与 children 按下标对齐，缺省权重 1。</summary>
[Serializable]
public sealed class RandomSelectorNodeDef : EnemyBehaviorNodeDef
{
    [SerializeReference] public List<EnemyBehaviorNodeDef> children = new List<EnemyBehaviorNodeDef>();

    [SerializeField] public List<float> weights = new List<float>();

    /// <inheritdoc />
    public override IBehaviorNode Build()
    {
        int count = children != null ? children.Count : 0;
        var built = new IBehaviorNode[count];
        var w = new float[count];
        for (int i = 0; i < count; i++)
        {
            built[i] = children[i] != null ? children[i].Build() : new StopMoveAction();
            w[i] = weights != null && i < weights.Count ? weights[i] : 1f;
        }

        return Wrap(new RandomSelectorNode(built, w));
    }

    /// <summary>保证 weights.Count ≥ children.Count（新增子缺省 1）。</summary>
    public void SyncWeightCount()
    {
        weights ??= new List<float>();
        int count = children != null ? children.Count : 0;
        while (weights.Count < count)
            weights.Add(1f);
        while (weights.Count > count)
            weights.RemoveAt(weights.Count - 1);
    }
}

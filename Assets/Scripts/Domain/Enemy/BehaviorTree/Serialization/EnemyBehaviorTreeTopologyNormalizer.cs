using System.Collections.Generic;

/// <summary>
/// 将「Sequence 下空 child 叶子 Condition」规范为 UE 装饰链（套到后续宿主上）。
/// 用于旧资产迁移；规范化后不再保留叶子条件双形态。
/// </summary>
public static class EnemyBehaviorTreeTopologyNormalizer
{
    /// <summary>就地规范化整棵树；有改动返回 true。</summary>
    public static bool Normalize(EnemyBehaviorNodeDef root)
    {
        if (root == null)
            return false;
        return NormalizeNode(root);
    }

    static bool NormalizeNode(EnemyBehaviorNodeDef node)
    {
        if (node == null)
            return false;

        bool changed = false;

        if (EnemyBehaviorTreeGraphMapper.TryGetChildren(node, out List<EnemyBehaviorNodeDef> children))
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] != null)
                    changed |= NormalizeNode(children[i]);
            }

            changed |= NormalizeCompositeChildren(children);
            return changed;
        }

        if (EnemyBehaviorTreeGraphMapper.TryGetSingleChild(node, out EnemyBehaviorNodeDef child)
            && child != null)
        {
            changed |= NormalizeNode(child);
        }

        return changed;
    }

    /// <summary>
    /// Sequence/Selector 子列表：空 child 装饰套到后续宿主；尾部悬空则套到前一个宿主。
    /// </summary>
    static bool NormalizeCompositeChildren(List<EnemyBehaviorNodeDef> children)
    {
        if (children == null || children.Count == 0)
            return false;

        // 常见旧形态：全部孤儿条件 + 全部宿主（条件可在任意下标）→ 按出现顺序套到 body
        if (TryNormalizeAllOrphansOntoHosts(children))
            return true;

        var rebuilt = new List<EnemyBehaviorNodeDef>(children.Count);
        bool changed = false;
        int i = 0;
        while (i < children.Count)
        {
            if (!IsOrphanDecorator(children[i]))
            {
                rebuilt.Add(children[i]);
                i++;
                continue;
            }

            var orphans = new List<EnemyBehaviorNodeDef>();
            while (i < children.Count && IsOrphanDecorator(children[i]))
            {
                orphans.Add(children[i]);
                i++;
            }

            if (i >= children.Count)
            {
                // 尾部悬空：套到已重建的最后一个宿主上
                if (rebuilt.Count > 0 && orphans.Count > 0)
                {
                    int last = rebuilt.Count - 1;
                    rebuilt[last] = NestDecorators(rebuilt[last], orphans);
                    changed = true;
                }
                else
                {
                    rebuilt.AddRange(orphans);
                }

                break;
            }

            var bodyParts = new List<EnemyBehaviorNodeDef>();
            while (i < children.Count && !IsOrphanDecorator(children[i]))
            {
                bodyParts.Add(children[i]);
                i++;
            }

            EnemyBehaviorNodeDef body = bodyParts.Count == 1
                ? bodyParts[0]
                : new SequenceNodeDef
                {
                    NodeName = "Body",
                    children = bodyParts,
                };

            rebuilt.Add(NestDecorators(body, orphans));
            changed = true;
        }

        if (!changed)
            return false;

        children.Clear();
        children.AddRange(rebuilt);
        return true;
    }

    /// <summary>
    /// 子列表仅含「孤儿装饰 + 非孤儿」两类时：按孤儿出现顺序套到全部非孤儿构成的 body。
    /// </summary>
    static bool TryNormalizeAllOrphansOntoHosts(List<EnemyBehaviorNodeDef> children)
    {
        var orphans = new List<EnemyBehaviorNodeDef>();
        var hosts = new List<EnemyBehaviorNodeDef>();
        for (int i = 0; i < children.Count; i++)
        {
            EnemyBehaviorNodeDef child = children[i];
            if (child == null)
                return false;
            if (IsOrphanDecorator(child))
                orphans.Add(child);
            else
                hosts.Add(child);
        }

        if (orphans.Count == 0 || hosts.Count == 0)
            return false;

        // 已有带 child 的装饰混在子列表时，勿做全局折叠（避免打乱已迁移分支）
        for (int i = 0; i < hosts.Count; i++)
        {
            if (IsDecoratorDef(hosts[i]) && !IsOrphanDecorator(hosts[i]))
                return false;
        }

        EnemyBehaviorNodeDef body = hosts.Count == 1
            ? hosts[0]
            : new SequenceNodeDef
            {
                NodeName = "Body",
                children = new List<EnemyBehaviorNodeDef>(hosts),
            };

        children.Clear();
        children.Add(NestDecorators(body, orphans));
        return true;
    }

    /// <summary>装饰定义且 child 为空（旧叶子条件形态）。</summary>
    public static bool IsOrphanDecorator(EnemyBehaviorNodeDef node)
    {
        if (!IsDecoratorDef(node))
            return false;
        return !EnemyBehaviorTreeGraphMapper.TryGetSingleChild(node, out EnemyBehaviorNodeDef child)
               || child == null;
    }

    /// <summary>是否为单子装饰 Def（条件或结构装饰）。</summary>
    public static bool IsDecoratorDef(EnemyBehaviorNodeDef node) =>
        node is EnemyBehaviorConditionNodeDef
        || node is InverterNodeDef
        || node is SucceederNodeDef
        || node is CooldownGateNodeDef;

    /// <summary>外→内套装饰；会写入各装饰 child。</summary>
    public static EnemyBehaviorNodeDef NestDecorators(
        EnemyBehaviorNodeDef inner,
        IReadOnlyList<EnemyBehaviorNodeDef> gatesOuterToInner)
    {
        EnemyBehaviorNodeDef current = inner;
        if (gatesOuterToInner == null || gatesOuterToInner.Count == 0)
            return current;

        for (int i = gatesOuterToInner.Count - 1; i >= 0; i--)
        {
            EnemyBehaviorNodeDef gate = gatesOuterToInner[i];
            if (gate == null || !IsDecoratorDef(gate))
                continue;
            SetChild(gate, current);
            current = gate;
        }

        return current;
    }

    static void SetChild(EnemyBehaviorNodeDef decorator, EnemyBehaviorNodeDef child)
    {
        switch (decorator)
        {
            case EnemyBehaviorConditionNodeDef condition:
                condition.child = child;
                break;
            case InverterNodeDef inverter:
                inverter.child = child;
                break;
            case SucceederNodeDef succeeder:
                succeeder.child = child;
                break;
            case CooldownGateNodeDef gate:
                gate.child = child;
                break;
        }
    }
}

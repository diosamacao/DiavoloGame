using System.Collections.Generic;

/// <summary>
/// UE 风格 Graph 表现：装饰/条件叠在宿主节点上；运行真源仍是单子装饰链。
/// </summary>
public static class EnemyBehaviorGraphPresentation
{
    /// <summary>画布上的宿主（Composite / Task）；装饰不单独成节点。</summary>
    public static bool IsHost(EnemyBehaviorNodeDef def) =>
        def != null && !EnemyBehaviorNodeCatalog.IsDecorator(def);

    /// <summary>
    /// 剥离外层装饰链：decorators 外→内，host 为最内非装饰节点。
    /// </summary>
    public static void Peel(
        EnemyBehaviorNodeDef node,
        out List<EnemyBehaviorNodeDef> decoratorsOuterToInner,
        out EnemyBehaviorNodeDef host)
    {
        decoratorsOuterToInner = new List<EnemyBehaviorNodeDef>();
        host = node;
        while (host != null
               && EnemyBehaviorNodeCatalog.IsDecorator(host)
               && EnemyBehaviorTreeGraphMapper.TryGetSingleChild(host, out EnemyBehaviorNodeDef child)
               && child != null)
        {
            decoratorsOuterToInner.Add(host);
            host = child;
        }
    }

    /// <summary>
    /// 将装饰外→内套回宿主，返回最外层（无装饰则返回宿主）。
    /// 会改写各装饰的 child 引用。
    /// </summary>
    public static EnemyBehaviorNodeDef Wrap(
        EnemyBehaviorNodeDef host,
        IReadOnlyList<EnemyBehaviorNodeDef> decoratorsOuterToInner)
    {
        EnemyBehaviorNodeDef current = host;
        if (decoratorsOuterToInner == null || decoratorsOuterToInner.Count == 0)
            return current;

        for (int i = decoratorsOuterToInner.Count - 1; i >= 0; i--)
        {
            EnemyBehaviorNodeDef gate = decoratorsOuterToInner[i];
            if (gate == null || !EnemyBehaviorNodeCatalog.IsDecorator(gate))
                continue;

            SetSingleChild(gate, current);
            current = gate;
        }

        return current;
    }

    /// <summary>写入装饰节点的唯一 child。</summary>
    public static void SetSingleChild(EnemyBehaviorNodeDef decorator, EnemyBehaviorNodeDef child)
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

    /// <summary>徽章显示名。</summary>
    public static string ChipLabel(EnemyBehaviorNodeDef def)
    {
        if (def == null)
            return "?";
        if (!string.IsNullOrEmpty(def.NodeName))
            return def.NodeName;
        return EnemyBehaviorTreeGraphMapper.DefaultNodeName(def);
    }
}

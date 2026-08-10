using System.Collections.Generic;

/// <summary>行为树资产 / 根校验结果。</summary>
public sealed class EnemyBehaviorTreeValidationResult
{
    readonly List<string> _errors = new List<string>();
    readonly List<string> _warnings = new List<string>();

    /// <summary>无 Error 即为通过（Warning 可保留）。</summary>
    public bool IsValid => _errors.Count == 0;

    /// <summary>错误列表。</summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>警告列表。</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>追加错误。</summary>
    public void AddError(string message)
    {
        if (!string.IsNullOrEmpty(message))
            _errors.Add(message);
    }

    /// <summary>追加警告。</summary>
    public void AddWarning(string message)
    {
        if (!string.IsNullOrEmpty(message))
            _warnings.Add(message);
    }
}

/// <summary>校验行为树结构、Guid 与会破坏动作等待闩的装饰拓扑。</summary>
public static class EnemyBehaviorTreeValidator
{
    /// <summary>校验整份行为树资产（必须已配置 customRoot）。</summary>
    public static EnemyBehaviorTreeValidationResult Validate(EnemyBehaviorTreeAsset asset)
    {
        var result = new EnemyBehaviorTreeValidationResult();
        if (asset == null)
        {
            result.AddError("资产为空。");
            return result;
        }

        if (asset.CustomRoot == null)
        {
            result.AddError("customRoot 为空：请在 Behavior Tree Editor 中手动配置节点并 Save。");
            return result;
        }

        ValidateTree(asset.CustomRoot, result);
        // Entry↔ActionGraph 可起手校验在 Editor 侧按 EnemyDefinition 配置链反查（见 CombatEntryPicker）

        if (asset.GraphLayout != null)
        {
            HashSet<string> live = EnemyBehaviorTreeGraphMapper.CollectGuids(asset.CustomRoot);
            for (int i = 0; i < asset.GraphLayout.Nodes.Count; i++)
            {
                EnemyBehaviorGraphNodeLayout layout = asset.GraphLayout.Nodes[i];
                if (layout == null || string.IsNullOrEmpty(layout.nodeGuid))
                {
                    result.AddWarning("GraphLayout 存在空 nodeGuid 条目。");
                    continue;
                }

                if (!live.Contains(layout.nodeGuid))
                    result.AddWarning($"GraphLayout 孤儿 guid：{layout.nodeGuid}");
            }
        }

        return result;
    }

    /// <summary>仅校验一棵 Def 树。</summary>
    public static EnemyBehaviorTreeValidationResult ValidateTree(EnemyBehaviorNodeDef root)
    {
        var result = new EnemyBehaviorTreeValidationResult();
        if (root == null)
        {
            result.AddError("根节点为空。");
            return result;
        }

        ValidateTree(root, result);
        return result;
    }

    /// <summary>初始化单次遍历状态并从根开始结构校验。</summary>
    static void ValidateTree(EnemyBehaviorNodeDef root, EnemyBehaviorTreeValidationResult result)
    {
        var pathStack = new HashSet<EnemyBehaviorNodeDef>();
        var guids = new HashSet<string>();
        Walk(root, result, pathStack, guids, "root", underLocomotionCondition: false);
    }

    /// <summary>深度遍历节点，同时传播 Locomotion 条件祖先标记用于 Wait 拓扑门禁。</summary>
    static void Walk(
        EnemyBehaviorNodeDef node,
        EnemyBehaviorTreeValidationResult result,
        HashSet<EnemyBehaviorNodeDef> pathStack,
        HashSet<string> guids,
        string path,
        bool underLocomotionCondition)
    {
        if (node == null)
        {
            result.AddError($"空节点：{path}");
            return;
        }

        if (!pathStack.Add(node))
        {
            result.AddError($"检测到环（同一节点实例重复引用）：{path} / {Describe(node)}");
            return;
        }

        if (!string.IsNullOrEmpty(node.NodeGuid) && !guids.Add(node.NodeGuid))
            result.AddError($"重复 NodeGuid：{node.NodeGuid} @ {path}");

        if (string.IsNullOrEmpty(node.NodeName))
            result.AddWarning($"缺少 NodeName：{path} / {Describe(node)}");

        if (node is RequestCombatActionDef request && string.IsNullOrEmpty(request.EntryNodeId))
            result.AddWarning($"RequestCombatAction Entry 为空：{path} / {Describe(node)}");

        if (node is WaitWhileInActionActionDef && underLocomotionCondition)
        {
            result.AddError(
                $"WaitWhileInAction 不得位于 IsCharacterState(Locomotion) 子树内：{path}/{Describe(node)}；"
                + "请把 Wait 移到 Locomotion 门控外层。");
        }

        bool childUnderLocomotionCondition = underLocomotionCondition
            || node is IsCharacterStateConditionDef stateCondition
            && stateCondition.Expected == CharacterStateType.Locomotion;

        if (EnemyBehaviorTreeGraphMapper.TryGetChildren(node, out List<EnemyBehaviorNodeDef> children))
        {
            if (children.Count == 0)
                result.AddWarning($"复合节点无子：{path} / {Describe(node)}");

            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] == null)
                    result.AddError($"空 child[{i}]：{path}/{Describe(node)}");
                else
                {
                    Walk(
                        children[i],
                        result,
                        pathStack,
                        guids,
                        $"{path}/{Describe(node)}[{i}]",
                        childUnderLocomotionCondition);
                }
            }

            pathStack.Remove(node);
            return;
        }

        if (EnemyBehaviorTreeGraphMapper.TryGetSingleChild(node, out EnemyBehaviorNodeDef child))
        {
            if (child == null)
                result.AddError($"装饰节点 child 为空：{path} / {Describe(node)}");
            else
            {
                Walk(
                    child,
                    result,
                    pathStack,
                    guids,
                    $"{path}/{Describe(node)}/child",
                    childUnderLocomotionCondition);
            }
        }

        pathStack.Remove(node);
    }

    /// <summary>
    /// 按给定 ActionGraph 校验 RequestCombatAction 的 Entry 可起手。
    /// Graph 由 Editor 从 EnemyDefinition → CombatProfile 反查后传入。
    /// </summary>
    public static void ValidateRequestCombatEntries(
        EnemyBehaviorNodeDef root,
        ActionGraph actionGraph,
        EnemyBehaviorTreeValidationResult result)
    {
        if (root == null || actionGraph == null || result == null)
            return;

        WalkRequestEntries(root, actionGraph, result, "root");
    }

    static void WalkRequestEntries(
        EnemyBehaviorNodeDef node,
        ActionGraph graph,
        EnemyBehaviorTreeValidationResult result,
        string path)
    {
        if (node == null)
            return;

        if (node is RequestCombatActionDef request
            && !string.IsNullOrEmpty(request.EntryNodeId))
        {
            if (!graph.TryGetNode(request.EntryNodeId, out ActionGraphNode graphNode)
                || !graphNode.IsEntry
                || graphNode.Action == null)
            {
                result.AddWarning(
                    $"RequestCombatAction Entry「{request.EntryNodeId}」在 ActionGraph「{graph.name}」上不可起手：{path}/{Describe(node)}");
            }
        }

        if (EnemyBehaviorTreeGraphMapper.TryGetChildren(node, out List<EnemyBehaviorNodeDef> children))
        {
            for (int i = 0; i < children.Count; i++)
                WalkRequestEntries(children[i], graph, result, $"{path}/{Describe(node)}[{i}]");
            return;
        }

        if (EnemyBehaviorTreeGraphMapper.TryGetSingleChild(node, out EnemyBehaviorNodeDef child))
            WalkRequestEntries(child, graph, result, $"{path}/{Describe(node)}/child");
    }

    static string Describe(EnemyBehaviorNodeDef node)
    {
        if (node == null)
            return "null";
        string name = string.IsNullOrEmpty(node.NodeName)
            ? EnemyBehaviorTreeGraphMapper.DefaultNodeName(node)
            : node.NodeName;
        return $"{name}<{node.GetType().Name}>";
    }
}

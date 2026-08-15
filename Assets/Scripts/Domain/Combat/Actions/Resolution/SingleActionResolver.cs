using System.Collections.Generic;
using UnityEngine;

/// <summary>单动作解析：始终返回固定 ActionDefinition，用于切模式、单段技能、单段闪避等。</summary>
[CreateAssetMenu(fileName = "SingleActionResolver", menuName = "ACT/Combat/Resolvers/Single Action Resolver")]
public class SingleActionResolver : ActionResolver
{
    [SerializeField] ActionDefinition action;

    public override bool TryResolve(
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionResolveResult result)
    {
        result = ActionResolveResult.FromAction(action);
        return result.IsValid;
    }

    /// <summary>登记固定动作，供复制目录 Prefill。</summary>
    public override void CollectActions(List<ActionDefinition> actions)
    {
        if (actions != null && action != null)
            actions.Add(action);
    }
}

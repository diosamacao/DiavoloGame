using UnityEngine;

/// <summary>单动作解析：始终返回固定 ActionDefinition，用于切模式、单段技能、单段闪避等。</summary>
[CreateAssetMenu(fileName = "SingleActionResolver", menuName = "ACT/Combat/Resolvers/Single Action Resolver")]
public class SingleActionResolver : ActionResolver
{
    [SerializeField] ActionDefinition action;

    public override bool TryResolve(
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionDefinition resolved)
    {
        resolved = action;
        return resolved != null && resolved.HasAnimation;
    }
}

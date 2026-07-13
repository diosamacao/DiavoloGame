using System;
using UnityEngine;

/// <summary>
/// 线性连段解析：Locomotion 起手与 Recovery Cancel 返回 steps[0]；
/// Action Cancel 按当前招在 steps 中的位置进位。
/// 无图游标；多窗差异派生与环请改用 ActionGraph。
/// </summary>
[CreateAssetMenu(fileName = "ComboActionResolver", menuName = "ACT/Combat/Resolvers/Combo Action Resolver")]
public class ComboActionResolver : ActionResolver
{
    [Tooltip("按顺序排列；steps[0] 为 Locomotion / Recovery Cancel 起手，Action Cancel 依次衔接后续段。")]
    [SerializeField] ActionDefinition[] steps = Array.Empty<ActionDefinition>();
    [SerializeField] ComboLeafPolicy leafPolicy = ComboLeafPolicy.LoopToRoot;

    public override bool TryResolve(
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionResolveResult result)
    {
        result = default;
        if (steps == null || steps.Length == 0)
            return false;

        ActionDefinition rootAction = steps[0];
        if (rootAction == null)
            return false;

        // 后摇 Cancel：不进位，重开连招首段。
        if (context.Origin == ActionResolveOrigin.CancelWindow
            && context.CancelType == CancelType.Recovery)
        {
            result = ActionResolveResult.FromAction(rootAction);
            return result.IsValid;
        }

        ActionDefinition current = context.CurrentAction;
        int index = current != null ? IndexOfStep(current) : -1;
        if (index < 0)
        {
            result = ActionResolveResult.FromAction(rootAction);
            return result.IsValid;
        }

        if (index + 1 < steps.Length)
        {
            result = ActionResolveResult.FromAction(steps[index + 1]);
            return result.IsValid;
        }

        if (leafPolicy == ComboLeafPolicy.LoopToRoot)
        {
            result = ActionResolveResult.FromAction(rootAction);
            return result.IsValid;
        }

        return false;
    }

    /// <summary>查找 current 在 steps 中的下标；未找到返回 -1。</summary>
    int IndexOfStep(ActionDefinition current)
    {
        for (int i = 0; i < steps.Length; i++)
        {
            if (steps[i] == current)
                return i;
        }

        return -1;
    }
}

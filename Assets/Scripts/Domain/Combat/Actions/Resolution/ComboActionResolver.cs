using System;
using UnityEngine;

/// <summary>
/// 线性连段解析：Locomotion 起手与 Recovery Cancel 返回 steps[0]；
/// Action Cancel 按当前招在 steps 中的位置进位。
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
        out ActionDefinition action)
    {
        action = null;
        if (steps == null || steps.Length == 0)
            return false;

        ActionDefinition rootAction = steps[0];
        if (rootAction == null)
            return false;

        // 后摇 Cancel：不进位，重开连招首段（去向由本 Resolver 决定，不写在 Action 时间轴上）。
        if (context.Origin == ActionResolveOrigin.CancelWindow
            && context.CancelType == CancelType.Recovery)
        {
            action = rootAction;
            return rootAction.HasAnimation;
        }

        // Locomotion 起手（current == null）或当前招不属于本连段：从首段起手。
        ActionDefinition current = context.CurrentAction;
        int index = current != null ? IndexOfStep(current) : -1;
        if (index < 0)
        {
            action = rootAction;
            return rootAction.HasAnimation;
        }

        // Action Cancel：进位到下一段。
        if (index + 1 < steps.Length)
        {
            action = steps[index + 1];
            return action != null && action.HasAnimation;
        }

        // 末段再次 Action Cancel：按叶策略循环回首段或终止连段。
        if (leafPolicy == ComboLeafPolicy.LoopToRoot)
        {
            action = rootAction;
            return rootAction.HasAnimation;
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

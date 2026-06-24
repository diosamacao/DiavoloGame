using System;
using UnityEngine;

/// <summary>连段队列末段再次输入时的行为。</summary>
public enum ComboLeafPolicy
{
    /// <summary>回到队列首段，继续循环连段。</summary>
    LoopToRoot = 0,

    /// <summary>不再衔接，Cancel 不生效。</summary>
    StopCombo = 1,
}

/// <summary>线性连招队列：Entry 绑定 input + 本序列；起手 = steps[0]，Cancel 按顺序进位。</summary>
[CreateAssetMenu(fileName = "ActionComboSequence", menuName = "ACT/Combat/Action Combo Sequence")]
public class ActionComboSequence : ScriptableObject
{
    [Tooltip("按顺序排列；steps[0] 为 Locomotion 起手，Cancel 依次衔接后续段。")]
    [SerializeField] ActionDefinition[] steps = Array.Empty<ActionDefinition>();
    [SerializeField] ComboLeafPolicy leafPolicy = ComboLeafPolicy.LoopToRoot;

    public ActionDefinition RootAction =>
        steps != null && steps.Length > 0 ? steps[0] : null;

    public ComboLeafPolicy LeafPolicy => leafPolicy;

    /// <summary>Locomotion 起手：队列第一段。</summary>
    public ActionDefinition GetStartAction() => RootAction;

    /// <summary>招内 Cancel：当前招在队列中则进位；不在队列中则回到首段。</summary>
    public bool TryResolveNext(string entryInputId, ActionDefinition current, out ActionDefinition next)
    {
        next = null;
        if (steps == null || steps.Length == 0 || string.IsNullOrEmpty(entryInputId))
            return false;

        ActionDefinition rootAction = steps[0];
        if (rootAction == null)
            return false;

        if (current == null)
        {
            next = rootAction;
            return true;
        }

        int index = IndexOfStep(current);
        if (index < 0)
        {
            next = rootAction;
            return true;
        }

        if (index + 1 < steps.Length)
        {
            next = steps[index + 1];
            return next != null && next.AnimationClip != null;
        }

        if (leafPolicy == ComboLeafPolicy.LoopToRoot)
        {
            next = rootAction;
            return true;
        }

        return false;
    }

    /// <summary>查找 current 在 steps 中的下标；未找到返回 -1。</summary>
    int IndexOfStep(ActionDefinition current)
    {
        if (current == null || steps == null)
            return -1;

        for (int i = 0; i < steps.Length; i++)
        {
            if (steps[i] == current)
                return i;
        }

        return -1;
    }
}

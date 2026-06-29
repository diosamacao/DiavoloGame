using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Locomotion 起手入口：Input System Action → ActionComboSequence（起手 = steps[0]）。</summary>
[Serializable]
public struct ActionEntry
{
    [Tooltip("从 GameInputActions 选择 Action（如 Player/Attack）；运行时 id = Action 名。")]
    [SerializeField] InputActionReference input;
    [SerializeField] ActionComboSequence comboSequence;

    public InputActionReference InputReference => input;
    public string InputId => InputBindingUtils.GetInputId(input);
    public ActionComboSequence ComboSequence => comboSequence;

    public bool IsValid =>
        InputBindingUtils.IsValid(input)
        && comboSequence != null
        && comboSequence.RootAction != null;
}

/// <summary>Dodge 方向变体配置：以根 Dodge Action 为键，按需提供前后左右替代动作。</summary>
[Serializable]
public class DodgeDirectionVariants
{
    [Tooltip("作为分派入口的根 Dodge Action（通常是 ComboSequence 的 RootAction）。")]
    [SerializeField] ActionDefinition rootDodgeAction;
    [Tooltip("仅派生窗口生效：输入与朝向夹角超过该阈值时，优先走左右。")]
    [SerializeField] float sideThresholdDeg = 80f;
    [Tooltip("非派生窗口前闪时是否先朝输入方向转向。")]
    [SerializeField] bool rotateToInputOnForward = true;
    [SerializeField] ActionDefinition forwardAction;
    [SerializeField] ActionDefinition backwardAction;
    [SerializeField] ActionDefinition leftAction;
    [SerializeField] ActionDefinition rightAction;

    /// <summary>作为方向分派入口的根 Dodge。</summary>
    public ActionDefinition RootDodgeAction => rootDodgeAction;
    /// <summary>左右判定阈值角度（0~180）。</summary>
    public float SideThresholdDeg => Mathf.Clamp(sideThresholdDeg, 0f, 180f);
    /// <summary>非派生窗口前闪时是否先朝输入方向转向。</summary>
    public bool RotateToInputOnForward => rotateToInputOnForward;
    /// <summary>前闪目标动作；为空时由执行器回退根 Dodge。</summary>
    public ActionDefinition ForwardAction => forwardAction;
    /// <summary>后闪目标动作；为空时由执行器回退根 Dodge。</summary>
    public ActionDefinition BackwardAction => backwardAction;
    /// <summary>左闪目标动作；为空时由执行器回退根 Dodge。</summary>
    public ActionDefinition LeftAction => leftAction;
    /// <summary>右闪目标动作；为空时由执行器回退根 Dodge。</summary>
    public ActionDefinition RightAction => rightAction;

    /// <summary>仅允许以 Dodge 作为分派入口，避免误将攻击招配置进来。</summary>
    public bool IsValid =>
        rootDodgeAction != null && rootDodgeAction.ActionType == CombatActionType.Dodge;
}

/// <summary>角色出招表：离散输入到起手招式的映射。</summary>
[CreateAssetMenu(fileName = "PlayerActionSet", menuName = "ACT/Combat/Player Action Set")]
public class PlayerActionSet : ScriptableObject
{
    [SerializeField] ActionEntry[] entries = Array.Empty<ActionEntry>();
    [SerializeField] DodgeDirectionVariants[] dodgeDirectionVariants = Array.Empty<DodgeDirectionVariants>();

    public System.Collections.Generic.IReadOnlyList<ActionEntry> Entries =>
        entries != null && entries.Length > 0 ? entries : Array.Empty<ActionEntry>();

    public bool TryGetStartAction(string inputId, out ActionDefinition startAction)
    {
        foreach (ActionEntry entry in Entries)
        {
            if (!entry.IsValid || entry.InputId != inputId)
                continue;

            startAction = entry.ComboSequence.RootAction;
            return true;
        }

        startAction = null;
        return false;
    }

    /// <summary>按根 Dodge Action 查找方向分派配置；未命中时返回 false。</summary>
    public bool TryGetDodgeDirectionVariants(
        ActionDefinition rootDodgeAction,
        out DodgeDirectionVariants variants)
    {
        if (rootDodgeAction == null || dodgeDirectionVariants == null)
        {
            variants = null;
            return false;
        }

        foreach (DodgeDirectionVariants entry in dodgeDirectionVariants)
        {
            if (entry == null || !entry.IsValid || entry.RootDodgeAction != rootDodgeAction)
                continue;

            variants = entry;
            return true;
        }

        variants = null;
        return false;
    }

    /// <summary>招内 Cancel：按 input 与当前招式在 Entry 的 ComboSequence 中顺序进位。</summary>
    public bool TryResolveNext(string inputId, ActionDefinition current, out ActionDefinition next)
    {
        next = null;
        if (string.IsNullOrEmpty(inputId))
            return false;

        foreach (ActionEntry entry in Entries)
        {
            if (!entry.IsValid || entry.InputId != inputId)
                continue;

            if (entry.ComboSequence.TryResolveNext(inputId, current, out next))
                return next != null;

            return false;
        }

        return false;
    }

    /// <summary>收集 Entries 中的离散 InputActionReference（按 Action 名去重）。</summary>
    public InputActionReference[] CollectEntryInputReferences()
    {
        if (entries == null || entries.Length == 0)
            return Array.Empty<InputActionReference>();

        var references = new InputActionReference[entries.Length];
        int count = 0;
        foreach (ActionEntry entry in entries)
        {
            if (!entry.IsValid)
                continue;

            references[count++] = entry.InputReference;
        }

        if (count == 0)
            return Array.Empty<InputActionReference>();

        if (count == references.Length)
            return InputBindingUtils.CollectUniqueReferences(references);

        var trimmed = new InputActionReference[count];
        Array.Copy(references, trimmed, count);
        return InputBindingUtils.CollectUniqueReferences(trimmed);
    }
}
using System;
using UnityEngine;
using UnityEngine.InputSystem;
/// <summary>Locomotion 起手入口：Input System Action → 起始 ActionDefinition。</summary>
[Serializable]
public struct ActionEntry
{
    [Tooltip("从 GameInputActions 选择 Action（如 Player/Attack）；运行时 id = Action 名。")]
    [SerializeField] InputActionReference input;
    [SerializeField] ActionDefinition startAction;

    public InputActionReference InputReference => input;
    public string InputId => InputBindingUtils.GetInputId(input);
    public ActionDefinition StartAction => startAction;

    public bool IsValid =>
        InputBindingUtils.IsValid(input) && startAction != null;
}

/// <summary>角色出招表：离散输入到起手招式的映射。</summary>
[CreateAssetMenu(fileName = "PlayerActionSet", menuName = "ACT/Combat/Player Action Set")]
public class PlayerActionSet : ScriptableObject
{
    [SerializeField] ActionEntry[] entries = Array.Empty<ActionEntry>();

    public System.Collections.Generic.IReadOnlyList<ActionEntry> Entries =>
        entries != null && entries.Length > 0 ? entries : Array.Empty<ActionEntry>();

    public bool TryGetStartAction(string inputId, out ActionDefinition startAction)
    {
        foreach (ActionEntry entry in Entries)
        {
            if (!entry.IsValid || entry.InputId != inputId)
                continue;

            startAction = entry.StartAction;
            return true;
        }

        startAction = null;
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
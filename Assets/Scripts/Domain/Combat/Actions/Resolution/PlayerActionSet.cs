using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>出招表入口：离散输入 → ActionResolver（起手、连段、方向分派统一由 Resolver 决策）。</summary>
[Serializable]
public struct ActionEntry
{
    [Tooltip("从 GameInputActions 选择 Action（如 Player/Attack）；运行时 id = Action 名。")]
    [SerializeField] InputActionReference input;
    [Tooltip("该输入的动作解析策略：Single / Combo / Directional。")]
    [SerializeField] ActionResolver resolver;

    public InputActionReference InputReference => input;
    public string InputId => InputBindingUtils.GetInputId(input);
    public ActionResolver Resolver => resolver;

    /// <summary>仅当输入引用与 Resolver 同时有效时才是有效入口。</summary>
    public bool IsValid => InputBindingUtils.IsValid(input) && resolver != null;
}

/// <summary>角色出招表：离散输入到 Resolver 的映射。</summary>
[CreateAssetMenu(fileName = "PlayerActionSet", menuName = "ACT/Combat/Player Action Set")]
public class PlayerActionSet : ScriptableObject
{
    [SerializeField] ActionEntry[] entries = Array.Empty<ActionEntry>();

    public System.Collections.Generic.IReadOnlyList<ActionEntry> Entries =>
        entries != null && entries.Length > 0 ? entries : Array.Empty<ActionEntry>();

    /// <summary>按输入 id 查找绑定的 Resolver；未命中返回 false。</summary>
    public bool TryGetResolver(string inputId, out ActionResolver resolver)
    {
        if (!string.IsNullOrEmpty(inputId))
        {
            foreach (ActionEntry entry in Entries)
            {
                if (!entry.IsValid || entry.InputId != inputId)
                    continue;

                resolver = entry.Resolver;
                return true;
            }
        }

        resolver = null;
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

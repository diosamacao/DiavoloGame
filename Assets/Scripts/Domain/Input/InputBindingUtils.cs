using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>解析并去重 GameplayIntentProfile 使用的 InputActionReference。</summary>
public static class InputBindingUtils
{
    public static bool IsValid(InputActionReference reference) =>
        reference != null && reference.action != null;

    public static string GetInputId(InputActionReference reference) =>
        IsValid(reference) ? reference.action.name : null;

    /// <summary>把物理输入引用转换为有效 Action 名。</summary>
    public static string[] ResolveInputIds(InputActionReference[] references)
    {
        if (references == null || references.Length == 0)
            return System.Array.Empty<string>();

        var ids = new List<string>(references.Length);
        foreach (InputActionReference reference in references)
        {
            string id = GetInputId(reference);
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }

        return ids.ToArray();
    }

    /// <summary>按 Action 名去重，收集有效 InputActionReference。</summary>
    public static InputActionReference[] CollectUniqueReferences(
        IEnumerable<InputActionReference> references)
    {
        if (references == null)
            return System.Array.Empty<InputActionReference>();

        var unique = new List<InputActionReference>();
        var seenIds = new HashSet<string>();

        foreach (InputActionReference reference in references)
        {
            if (!IsValid(reference))
                continue;

            string id = reference.action.name;
            if (!seenIds.Add(id))
                continue;

            unique.Add(reference);
        }

        return unique.ToArray();
    }
}

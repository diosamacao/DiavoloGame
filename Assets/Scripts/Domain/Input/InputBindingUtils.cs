using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>解析并去重 GameplayIntentProfile 使用的 InputActionReference。</summary>
public static class InputBindingUtils
{
    public static bool IsValid(InputActionReference reference) =>
        reference != null && reference.action != null;

    /// <summary>在 Unity 设备边界把 Action 名映射为稳定 InputButton bit。</summary>
    public static bool TryGetButton(InputActionReference reference, out InputButton button)
    {
        if (!IsValid(reference))
        {
            button = default;
            return false;
        }

        switch (reference.action.name)
        {
            case "Attack":
                button = InputButton.Attack;
                return true;
            case "Dodge":
                button = InputButton.Dodge;
                return true;
            case "SwitchMode":
                button = InputButton.SwitchMode;
                return true;
            case "HeavyAttack":
                button = InputButton.HeavyAttack;
                return true;
            case "Evade":
                button = InputButton.Evade;
                return true;
            case "Skill":
                button = InputButton.Skill;
                return true;
            case "TargetSwitchLeft":
                button = InputButton.TargetSwitchLeft;
                return true;
            case "TargetSwitchRight":
                button = InputButton.TargetSwitchRight;
                return true;
            case "Ultimate":
                button = InputButton.Ultimate;
                return true;
            case "SwitchCharacter":
                button = InputButton.SwitchCharacter;
                return true;
            default:
                button = default;
                return false;
        }
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

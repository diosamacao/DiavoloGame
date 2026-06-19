using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Locomotion 起手入口：输入 id → 起始 ActionDefinition。</summary>
[Serializable]
public struct ActionEntry
{
    [SerializeField] string inputId;
    [SerializeField] ActionDefinition startAction;

    public ActionEntry(string inputId, ActionDefinition startAction)
    {
        this.inputId = inputId;
        this.startAction = startAction;
    }

    public string InputId => inputId;
    public ActionDefinition StartAction => startAction;

    public bool IsValid => !string.IsNullOrEmpty(inputId) && startAction != null;
}

/// <summary>角色出招表：离散输入 id 到起手招式的映射。</summary>
[CreateAssetMenu(fileName = "PlayerActionSet", menuName = "ACT/Combat/Player Action Set")]
public class PlayerActionSet : ScriptableObject
{
    [SerializeField] ActionEntry[] entries = Array.Empty<ActionEntry>();

    public IReadOnlyList<ActionEntry> Entries =>
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
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>战斗模式配置：mode → ActionGraph + LocomotionProfile。</summary>
[Serializable]
public sealed class CharacterCombatModes
{
    [SerializeField] CombatModeType defaultMode = CombatModeType.Default;
    [SerializeField] CombatModeEntry[] entries = Array.Empty<CombatModeEntry>();

    /// <summary>进入运行时时的默认模式。</summary>
    public CombatModeType DefaultMode => defaultMode;

    /// <summary>全部模式条目；复制目录预填时遍历 Graph，不依赖登记顺序。</summary>
    public IReadOnlyList<CombatModeEntry> Entries => entries ?? Array.Empty<CombatModeEntry>();

    /// <summary>查找指定模式的 ActionGraph。</summary>
    public bool TryGetActionGraph(CombatModeType mode, out ActionGraph actionGraph)
    {
        if (entries != null)
        {
            foreach (CombatModeEntry entry in entries)
            {
                if (!entry.IsValid || entry.Mode != mode)
                    continue;

                actionGraph = entry.ActionGraph;
                return true;
            }
        }

        actionGraph = null;
        return false;
    }

    /// <summary>查找指定模式的 LocomotionProfile。</summary>
    public bool TryGetLocomotionProfile(CombatModeType mode, out CharacterLocomotionProfile locomotionProfile)
    {
        if (entries != null)
        {
            foreach (CombatModeEntry entry in entries)
            {
                if (!entry.IsValid || entry.Mode != mode)
                    continue;

                locomotionProfile = entry.LocomotionProfile;
                return true;
            }
        }

        locomotionProfile = null;
        return false;
    }

    /// <summary>启动期校验全部模式的唯一性、Graph Action 与完整 Locomotion 内容。</summary>
    public bool Validate(UnityEngine.Object context)
    {
        UnityEngine.Object logContext = context;
        if (entries == null || entries.Length == 0)
        {
            Debug.LogError("CharacterCombatModes: 至少需要一个模式条目。", logContext);
            return false;
        }

        bool valid = true;
        bool foundDefault = false;
        var seenModes = new HashSet<CombatModeType>();
        for (int i = 0; i < entries.Length; i++)
        {
            CombatModeEntry entry = entries[i];
            if (!seenModes.Add(entry.Mode))
            {
                Debug.LogError($"CharacterCombatModes: 模式 {entry.Mode} 重复。", logContext);
                valid = false;
            }
            if (!entry.IsValid)
            {
                Debug.LogError(
                    $"CharacterCombatModes: 条目[{i}] 必须同时配置 ActionGraph 与 LocomotionProfile。",
                    logContext);
                valid = false;
                continue;
            }

            foundDefault |= entry.Mode == DefaultMode;
            valid &= ValidateGraphActions(entry.ActionGraph, logContext);
            valid &= entry.LocomotionProfile.Validate(context);
        }

        if (!foundDefault)
        {
            Debug.LogError(
                $"CharacterCombatModes: defaultMode={DefaultMode} 没有对应有效条目。",
                logContext);
            valid = false;
        }
        return valid;
    }

    /// <summary>角色启动与 Editor 共用动作图结构门禁。</summary>
    static bool ValidateGraphActions(ActionGraph graph, UnityEngine.Object context) =>
        ActionGraphValidator.ValidateAndLog(graph);
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>战斗模式标识；每种模式绑定一张 ActionGraph 与一套 LocomotionProfile。</summary>
public enum CombatModeType
{
    Default = 0,
    Katana = 1,
    Beast = 2,
}

/// <summary>切换战斗模式时的时机策略。</summary>
public enum CombatModeSwitchPolicy
{
    /// <summary>立即切换出招图；当前招式继续播放。</summary>
    Immediate = 0,

    /// <summary>若正在招式中则延迟，回到 Locomotion 后再切换。</summary>
    OnNextLocomotion = 1,

    /// <summary>Stop 当前招式后立即切换。</summary>
    StopCurrentAction = 2,
}

/// <summary>单个战斗模式：ActionGraph + 必填 LocomotionProfile（内含 AnimationProfile）。</summary>
[Serializable]
public struct CombatModeEntry
{
    [SerializeField] CombatModeType mode;
    [Tooltip("本模式出招图（Entry×Intent / Cancel）。")]
    [SerializeField] ActionGraph actionGraph;
    [FormerlySerializedAs("animationProfile")]
    [Tooltip("本模式 Locomotion（含 Clip 映射与相位参数）；必填。")]
    [SerializeField] CharacterLocomotionProfile locomotionProfile;

    /// <summary>模式枚举。</summary>
    public CombatModeType Mode => mode;

    /// <summary>本模式 ActionGraph。</summary>
    public ActionGraph ActionGraph => actionGraph;

    /// <summary>本模式完整 Locomotion 配置。</summary>
    public CharacterLocomotionProfile LocomotionProfile => locomotionProfile;

    /// <summary>Graph 与 LocomotionProfile 均已绑定。</summary>
    public bool IsValid => actionGraph != null && locomotionProfile != null;
}

/// <summary>战斗模式配置：mode → ActionGraph + LocomotionProfile。</summary>
[CreateAssetMenu(fileName = "CombatModeProfile", menuName = "ACT/Combat/Combat Mode Profile")]
public class CombatModeProfile : ScriptableObject
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
        UnityEngine.Object logContext = context != null ? context : this;
        if (entries == null || entries.Length == 0)
        {
            Debug.LogError("CombatModeProfile: 至少需要一个模式条目。", logContext);
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
                Debug.LogError($"CombatModeProfile: 模式 {entry.Mode} 重复。", logContext);
                valid = false;
            }
            if (!entry.IsValid)
            {
                Debug.LogError(
                    $"CombatModeProfile: 条目[{i}] 必须同时配置 ActionGraph 与 LocomotionProfile。",
                    logContext);
                valid = false;
                continue;
            }

            foundDefault |= entry.Mode == DefaultMode;
            valid &= ValidateGraphActions(entry.ActionGraph, logContext);
            valid &= entry.LocomotionProfile.Validate(this);
        }

        if (!foundDefault)
        {
            Debug.LogError(
                $"CombatModeProfile: defaultMode={DefaultMode} 没有对应有效条目。",
                logContext);
            valid = false;
        }
        return valid;
    }

    /// <summary>每个 Graph 必须有节点，且节点必须绑定 ActionDefinition。</summary>
    static bool ValidateGraphActions(ActionGraph graph, UnityEngine.Object context)
    {
        IReadOnlyList<ActionGraphNode> nodes = graph.Nodes;
        if (nodes.Count == 0)
        {
            Debug.LogError($"CombatModeProfile: ActionGraph '{graph.name}' 没有节点。", context);
            return false;
        }

        bool valid = true;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && nodes[i].Action != null)
                continue;
            Debug.LogError(
                $"CombatModeProfile: ActionGraph '{graph.name}' 的节点[{i}] 未绑定 ActionDefinition。",
                context);
            valid = false;
        }
        return valid;
    }
}

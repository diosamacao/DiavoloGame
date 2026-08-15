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

    /// <summary>校验默认模式：Graph + LocomotionProfile（含 AnimationProfile / Idle·Walk·Run）。</summary>
    public bool Validate(UnityEngine.Object context)
    {
        if (!TryGetActionGraph(DefaultMode, out _)
            || !TryGetLocomotionProfile(DefaultMode, out CharacterLocomotionProfile loco))
        {
            Debug.LogError(
                $"CombatModeProfile: defaultMode={DefaultMode} 必须同时配置 ActionGraph 与 LocomotionProfile。",
                context != null ? context : this);
            return false;
        }

        return loco.Validate(context != null ? context : this);
    }
}

using System;
using UnityEngine;

/// <summary>战斗模式标识；每种模式绑定一张 ActionGraph。</summary>
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

/// <summary>单个战斗模式与 ActionGraph、Locomotion 动画配置的绑定项。</summary>
[Serializable]
public struct CombatModeEntry
{
    [SerializeField] CombatModeType mode;
    [Tooltip("本模式出招图（Entry×Intent / Cancel）；不再经 PlayerActionSet。")]
    [SerializeField] ActionGraph actionGraph;
    [Tooltip("该模式的 Idle/Walk/Run 映射；为空则切换到此 mode 时不改 Locomotion Profile。")]
    [SerializeField] CharacterAnimationProfile locomotionProfile;

    /// <summary>模式枚举。</summary>
    public CombatModeType Mode => mode;

    /// <summary>本模式 ActionGraph。</summary>
    public ActionGraph ActionGraph => actionGraph;

    /// <summary>可选 Locomotion Clip 映射。</summary>
    public CharacterAnimationProfile LocomotionProfile => locomotionProfile;

    /// <summary>已绑定有效 ActionGraph。</summary>
    public bool IsValid => actionGraph != null;
}

/// <summary>战斗模式配置：mode → ActionGraph / 可选 Locomotion Profile。</summary>
[CreateAssetMenu(fileName = "CombatModeProfile", menuName = "ACT/Combat/Combat Mode Profile")]
public class CombatModeProfile : ScriptableObject
{
    [SerializeField] CombatModeType defaultMode = CombatModeType.Default;
    [SerializeField] CombatModeEntry[] entries = Array.Empty<CombatModeEntry>();

    /// <summary>进入运行时时的默认模式。</summary>
    public CombatModeType DefaultMode => defaultMode;

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

    /// <summary>查找指定模式的 Locomotion 动画 Profile；条目存在但 profile 为空时返回 false。</summary>
    public bool TryGetLocomotionProfile(CombatModeType mode, out CharacterAnimationProfile locomotionProfile)
    {
        if (entries != null)
        {
            foreach (CombatModeEntry entry in entries)
            {
                if (entry.Mode != mode || !entry.IsValid)
                    continue;

                locomotionProfile = entry.LocomotionProfile;
                return locomotionProfile != null;
            }
        }

        locomotionProfile = null;
        return false;
    }
}

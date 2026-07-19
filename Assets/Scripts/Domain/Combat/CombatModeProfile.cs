using System;
using UnityEngine;

/// <summary>战斗模式标识：每种模式绑定独立 PlayerActionSet 出招表。</summary>
public enum CombatModeType
{
    Default = 0,
    Katana = 1,
    Beast = 2,
}

/// <summary>切换战斗模式时的时机策略。</summary>
public enum CombatModeSwitchPolicy
{
    /// <summary>立即切换出招表；当前招式继续播放。</summary>
    Immediate = 0,

    /// <summary>若正在招式中则延迟，回到 Locomotion 后再切换。</summary>
    OnNextLocomotion = 1,

    /// <summary>Stop 当前招式后立即切换。</summary>
    StopCurrentAction = 2,
}

/// <summary>单个战斗模式与出招表、Locomotion 动画配置的绑定项。</summary>
[Serializable]
public struct CombatModeEntry
{
    [SerializeField] CombatModeType mode;
    [SerializeField] PlayerActionSet actionSet;
    [Tooltip("该模式的 Idle/Walk/Run 映射；为空则切换到此 mode 时不改 Locomotion Profile。")]
    [SerializeField] CharacterAnimationProfile locomotionProfile;

    public CombatModeType Mode => mode;
    public PlayerActionSet ActionSet => actionSet;
    public CharacterAnimationProfile LocomotionProfile => locomotionProfile;

    public bool IsValid => actionSet != null && actionSet.IsValid;
}

/// <summary>战斗模式配置：mode → PlayerActionSet（内含 ActionGraph）/ Locomotion Profile。</summary>
[CreateAssetMenu(fileName = "CombatModeProfile", menuName = "ACT/Combat/Combat Mode Profile")]
public class CombatModeProfile : ScriptableObject
{
    [SerializeField] CombatModeType defaultMode = CombatModeType.Default;
    [SerializeField] CombatModeEntry[] entries = Array.Empty<CombatModeEntry>();

    public CombatModeType DefaultMode => defaultMode;

    /// <summary>查找指定模式的出招表。</summary>
    public bool TryGetActionSet(CombatModeType mode, out PlayerActionSet actionSet)
    {
        if (entries != null)
        {
            foreach (CombatModeEntry entry in entries)
            {
                if (!entry.IsValid || entry.Mode != mode)
                    continue;

                actionSet = entry.ActionSet;
                return true;
            }
        }

        actionSet = null;
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

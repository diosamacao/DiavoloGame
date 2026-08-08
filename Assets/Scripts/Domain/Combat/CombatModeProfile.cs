using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>战斗模式标识；每种模式绑定一张 ActionGraph 与 Locomotion 动画映射。</summary>
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

/// <summary>单个战斗模式：ActionGraph + 必填 AnimationProfile（Idle/Walk/Run Clip 映射）。</summary>
[Serializable]
public struct CombatModeEntry
{
    [SerializeField] CombatModeType mode;
    [Tooltip("本模式出招图（Entry×Intent / Cancel）。")]
    [SerializeField] ActionGraph actionGraph;
    [FormerlySerializedAs("locomotionProfile")]
    [Tooltip("本模式 Idle/Walk/Run 等 Clip 映射；必填，不再回退 CharacterConfig。")]
    [SerializeField] CharacterAnimationProfile animationProfile;

    /// <summary>模式枚举。</summary>
    public CombatModeType Mode => mode;

    /// <summary>本模式 ActionGraph。</summary>
    public ActionGraph ActionGraph => actionGraph;

    /// <summary>本模式 Locomotion Clip 映射。</summary>
    public CharacterAnimationProfile AnimationProfile => animationProfile;

    /// <summary>Graph 与 AnimationProfile 均已绑定。</summary>
    public bool IsValid => actionGraph != null && animationProfile != null;
}

/// <summary>战斗模式配置：mode → ActionGraph + AnimationProfile。</summary>
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

    /// <summary>查找指定模式的 Locomotion Clip 映射（AnimationProfile）。</summary>
    public bool TryGetAnimationProfile(CombatModeType mode, out CharacterAnimationProfile animationProfile)
    {
        if (entries != null)
        {
            foreach (CombatModeEntry entry in entries)
            {
                if (!entry.IsValid || entry.Mode != mode)
                    continue;

                animationProfile = entry.AnimationProfile;
                return true;
            }
        }

        animationProfile = null;
        return false;
    }

    /// <summary>校验默认模式条目完整（Graph + AnimationProfile）。</summary>
    public bool Validate(UnityEngine.Object context)
    {
        if (!TryGetActionGraph(DefaultMode, out _) || !TryGetAnimationProfile(DefaultMode, out CharacterAnimationProfile anim))
        {
            Debug.LogError(
                $"CombatModeProfile: defaultMode={DefaultMode} 必须同时配置 ActionGraph 与 AnimationProfile。",
                context != null ? context : this);
            return false;
        }

        return anim.ValidateClips(context != null ? context : this);
    }
}

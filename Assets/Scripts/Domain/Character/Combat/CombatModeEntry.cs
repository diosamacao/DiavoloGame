using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>单个战斗模式：ActionGraph + 必填 LocomotionProfile（内含动画映射）。</summary>
[Serializable]
public struct CombatModeEntry
{
    [SerializeField] CombatModeType mode;
    [Tooltip("本模式出招图（Entry×Intent / Cancel）。")]
    [SerializeField] ActionGraph actionGraph;
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


using System;
using UnityEngine;

/// <summary>动作解析来源：区分起手、显式 Cancel、Recovery Entry 与高优硬打断。</summary>
public enum ActionResolveOrigin
{
    /// <summary>从 Locomotion 起手解析（当前无激活招式）。</summary>
    LocomotionStart = 0,

    /// <summary>从当前招式的 CancelWindow 内解析下一招。</summary>
    CancelWindow = 1,

    /// <summary>Action 态高优硬打断：按 Graph Entry 解析候选招。</summary>
    PriorityInterrupt = 2,

    /// <summary>当前招式处于 Recovery Phase：按 Graph Entry 软切换，不要求显式图边。</summary>
    RecoveryEntry = 3,
}

using System;
using UnityEngine;

/// <summary>动作自身固定的执行规则；不包含输入、选招、索敌或命中反馈策略。</summary>
[Serializable]
public sealed class ActionExecutionPolicy
{
    [Tooltip("动作打断优先级；更大则可硬打断更小者，同级不互相打断。")]
    [SerializeField] int interruptPriority = 0;
    [Tooltip("开启时由动画 Root Motion 驱动位移，脚本位移窗口将被忽略（仅 LegacyResolve 迁移窗口读取）。")]
    [SerializeField] bool useRootMotion = true;

    [Tooltip("Wave 1：基础位移权威。LegacyResolve=未迁移；迁移工具写入 None/Baked/Scripted。")]
    [SerializeField] ActionBaseMotionMode baseMotionMode = ActionBaseMotionMode.LegacyResolve;

    /// <summary>动作打断优先级。</summary>
    public int InterruptPriority => interruptPriority;

    /// <summary>是否由动画 Root Motion 驱动位移（迁移期只读观测 / Legacy 回退）。</summary>
    public bool UseRootMotion => useRootMotion;

    /// <summary>基础位移模式；未迁移资产为 LegacyResolve。</summary>
    public ActionBaseMotionMode BaseMotionMode => baseMotionMode;

    /// <summary>Editor 迁移写回 BaseMotionMode；运行时不得调用。</summary>
    public void EditorSetBaseMotionMode(ActionBaseMotionMode mode) => baseMotionMode = mode;
}

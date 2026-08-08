using System;
using UnityEngine;

/// <summary>动作自身固定的执行规则；不包含输入、选招、索敌或命中反馈策略。</summary>
[Serializable]
public sealed class ActionExecutionPolicy
{
    [Tooltip("动作打断优先级；更大则可硬打断更小者，同级不互相打断。")]
    [SerializeField] int interruptPriority = 0;

    [Tooltip("基础位移权威：None / BakedMotion / ScriptedTimeline（无 Animator RM）。")]
    [SerializeField] ActionBaseMotionMode baseMotionMode = ActionBaseMotionMode.None;

    /// <summary>动作打断优先级。</summary>
    public int InterruptPriority => interruptPriority;

    /// <summary>基础位移模式。</summary>
    public ActionBaseMotionMode BaseMotionMode => baseMotionMode;

    /// <summary>Editor 写回 BaseMotionMode；运行时不得调用。</summary>
    public void EditorSetBaseMotionMode(ActionBaseMotionMode mode) => baseMotionMode = mode;
}

using System;
using UnityEngine;

/// <summary>动作自身固定的执行规则；不包含输入、选招、索敌或命中反馈策略。</summary>
[Serializable]
public sealed class ActionExecutionPolicy
{
    [Tooltip("动作打断优先级；更大则可硬打断更小者，同级不互相打断。")]
    [SerializeField] int interruptPriority = 0;
    [Tooltip("开启时由动画 Root Motion 驱动位移，脚本位移窗口将被忽略。")]
    [SerializeField] bool useRootMotion = true;

    /// <summary>动作打断优先级。</summary>
    public int InterruptPriority => interruptPriority;

    /// <summary>是否由动画 Root Motion 驱动位移。</summary>
    public bool UseRootMotion => useRootMotion;
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>单条物理输入到玩法意图的映射及其上下文限制。</summary>
[Serializable]
public struct GameplayIntentBinding
{
    [SerializeField] InputActionReference input;
    [SerializeField] GameplayIntentInputPhase phase;
    [SerializeField] GameplayIntentType intent;
    [SerializeField] GameplayIntentCondition condition;
    [Tooltip("仅 HoldReached 使用；小于等于 0 时按 21 帧（60Hz 下 0.35 秒）。")]
    [SerializeField] int holdFrames;
    [Tooltip("同一物理事件有多个匹配时，数值更大的映射优先；同值时上下文条件比 Always 优先。")]
    [SerializeField] int priority;

    /// <summary>物理 InputAction 引用。</summary>
    public InputActionReference Input => input;
    /// <summary>由设备边界 Action 名映射出的稳定按钮 bit。</summary>
    public InputButton Button =>
        InputBindingUtils.TryGetButton(input, out InputButton button) ? button : default;
    /// <summary>按下、达到长按阈值或松开的映射时机。</summary>
    public GameplayIntentInputPhase Phase => phase;
    /// <summary>匹配成功后输出的设备无关意图。</summary>
    public GameplayIntentType Intent => intent;
    /// <summary>映射生效所需的角色上下文。</summary>
    public GameplayIntentCondition Condition => condition;
    /// <summary>长按阈值逻辑帧数；未配置时为 21 帧。</summary>
    public int HoldFrames => holdFrames > 0 ? holdFrames : 21;
    /// <summary>同一物理事件内的显式优先级。</summary>
    public int Priority => priority;
    /// <summary>物理引用有效且输出意图不是 None。</summary>
    public bool IsValid =>
        InputBindingUtils.TryGetButton(input, out _) && intent != GameplayIntentType.None;
}

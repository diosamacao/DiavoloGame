using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>物理 InputAction 到设备无关玩法意图的映射配置。</summary>
[CreateAssetMenu(fileName = "GameplayIntentProfile", menuName = "ACT/Input/Gameplay Intent Profile")]
public sealed class GameplayIntentProfile : ScriptableObject
{
    [SerializeField] GameplayIntentBinding[] bindings = Array.Empty<GameplayIntentBinding>();
    [Tooltip("Action 内输入缓冲有效期（秒）；小于等于 0 时使用 0.15 秒。")]
    [SerializeField] float actionBufferDurationSeconds = 0.15f;

    /// <summary>全部意图映射；运行时只读取有效项。</summary>
    public IReadOnlyList<GameplayIntentBinding> Bindings =>
        bindings ?? Array.Empty<GameplayIntentBinding>();

    /// <summary>Action Cancel / Recovery 预输入的统一有效期，避免早期输入在动作结束后误触发。</summary>
    public float ActionBufferDurationSeconds =>
        actionBufferDurationSeconds > 0f ? actionBufferDurationSeconds : 0.15f;

    /// <summary>收集需要 InputReader 轮询的物理输入引用。</summary>
    public InputActionReference[] CollectInputReferences()
    {
        var references = new List<InputActionReference>(Bindings.Count);
        for (int i = 0; i < Bindings.Count; i++)
        {
            GameplayIntentBinding binding = Bindings[i];
            if (binding.IsValid)
                references.Add(binding.Input);
        }

        return InputBindingUtils.CollectUniqueReferences(references);
    }
}

/// <summary>单条物理输入到玩法意图的映射及其上下文限制。</summary>
[Serializable]
public struct GameplayIntentBinding
{
    [SerializeField] InputActionReference input;
    [SerializeField] GameplayIntentInputPhase phase;
    [SerializeField] GameplayIntentType intent;
    [SerializeField] GameplayIntentCondition condition;
    [Tooltip("仅 HoldReached 使用；小于等于 0 时按 0.35 秒。")]
    [SerializeField] float holdSeconds;
    [Tooltip("同一物理事件有多个匹配时，数值更大的映射优先；同值时上下文条件比 Always 优先。")]
    [SerializeField] int priority;

    /// <summary>物理 InputAction 引用。</summary>
    public InputActionReference Input => input;
    /// <summary>物理 Action 名，仅用于原始帧匹配。</summary>
    public string InputId => InputBindingUtils.GetInputId(input);
    /// <summary>按下、达到长按阈值或松开的映射时机。</summary>
    public GameplayIntentInputPhase Phase => phase;
    /// <summary>匹配成功后输出的设备无关意图。</summary>
    public GameplayIntentType Intent => intent;
    /// <summary>映射生效所需的角色上下文。</summary>
    public GameplayIntentCondition Condition => condition;
    /// <summary>长按阈值；未配置时为 0.35 秒。</summary>
    public float HoldSeconds => holdSeconds > 0f ? holdSeconds : 0.35f;
    /// <summary>同一物理事件内的显式优先级。</summary>
    public int Priority => priority;
    /// <summary>物理引用有效且输出意图不是 None。</summary>
    public bool IsValid => InputBindingUtils.IsValid(input) && intent != GameplayIntentType.None;
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>连招图节点：拥有动作选择意图、上下文执行策略与自动衔接拓扑。</summary>
[Serializable]
public class ActionGraphNode
{
    [SerializeField] string nodeId = string.Empty;
    [SerializeField] ActionDefinition action = null;
    [Tooltip("输入选招时匹配的玩法意图；行为树按 NodeId 起手可留 None。多个 None Entry 合法；作为输入 Cancel 连线目标时需要非空 Intent。")]
    [SerializeField] GameplayIntentType intent = GameplayIntentType.None;
    [Tooltip("允许从此节点起手：玩家按 Intent 选择，行为树按 NodeId 选择。行为树入口无需配置 Intent。")]
    [SerializeField] bool isEntry = false;
    [Tooltip("可选：进入本节点前解析实际播放变体（如 Directional 闪避）；变体共用当前逻辑节点和出边。")]
    [SerializeField] ActionResolver variantResolver = null;
    [Tooltip("进入节点时执行的上下文行为；直接播放 ActionDefinition 时不会执行。")]
    [SerializeField] ActionGraphStartBehaviorType[] startBehaviors =
        Array.Empty<ActionGraphStartBehaviorType>();
    [Tooltip("Start Behaviors 包含 SwitchCombatMode 时使用。")]
    [SerializeField] CombatModeType switchCombatModeTarget = CombatModeType.Default;
    [SerializeField] CombatModeSwitchPolicy switchCombatModePolicy =
        CombatModeSwitchPolicy.Immediate;
    [Tooltip("当前图节点的索敌策略；同一 Action 可在不同节点使用不同策略。")]
    [SerializeField] TargetLockSettings targetLockSettings = new();
    [Tooltip("无输入自动衔接规则；流程拓扑只保存在 ActionGraph。")]
    [SerializeField] ActionGraphTransition[] automaticTransitions =
        Array.Empty<ActionGraphTransition>();
    [SerializeField] Vector2 editorPosition = Vector2.zero;

    /// <summary>图内唯一节点 id。</summary>
    public string NodeId => nodeId;

    /// <summary>本节点播放的招式。</summary>
    public ActionDefinition Action => action;

    /// <summary>进入节点所匹配的玩法意图。</summary>
    public GameplayIntentType Intent => intent;

    /// <summary>是否可作为 Locomotion 起手入口。</summary>
    public bool IsEntry => isEntry;

    /// <summary>进入时可选的变体 Resolver（Directional 等）。</summary>
    public ActionResolver VariantResolver => variantResolver;

    /// <summary>进入节点时执行的上下文行为。</summary>
    public IReadOnlyList<ActionGraphStartBehaviorType> StartBehaviors =>
        startBehaviors ?? Array.Empty<ActionGraphStartBehaviorType>();

    /// <summary>节点要求切换到的战斗模式。</summary>
    public CombatModeType SwitchCombatModeTarget => switchCombatModeTarget;

    /// <summary>节点切换战斗模式的时机策略。</summary>
    public CombatModeSwitchPolicy SwitchCombatModePolicy => switchCombatModePolicy;

    /// <summary>节点级索敌策略。</summary>
    public TargetLockSettings TargetLockSettings =>
        targetLockSettings ?? new TargetLockSettings();

    /// <summary>节点是否启用索敌。</summary>
    public bool HasTargetLock => targetLockSettings != null && targetLockSettings.Enabled;

    /// <summary>节点的无输入自动衔接规则。</summary>
    public IReadOnlyList<ActionGraphTransition> AutomaticTransitions =>
        automaticTransitions ?? Array.Empty<ActionGraphTransition>();

    /// <summary>编辑器布局位置。</summary>
    public Vector2 EditorPosition => editorPosition;

}

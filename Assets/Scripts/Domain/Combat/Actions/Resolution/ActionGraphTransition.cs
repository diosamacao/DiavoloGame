using System;
using UnityEngine;

/// <summary>动作图节点的自动衔接规则；目标使用节点 Id，避免动作资源持有流程拓扑。</summary>
[Serializable]
public sealed class ActionGraphTransition
{
    [SerializeField] ActionTransitionCondition condition = ActionTransitionCondition.AnimationEnd;
    [Tooltip("AtFrame 条件达到该帧（含）后触发，其它条件忽略此值。")]
    [SerializeField] int startFrame = 0;
    [Tooltip("目标节点 Id；留空表示满足条件时结束当前动作。")]
    [SerializeField] string targetNodeId = string.Empty;
    [SerializeField] int priority = 0;

    /// <summary>自动衔接条件。</summary>
    public ActionTransitionCondition Condition => condition;

    /// <summary>AtFrame 条件的起始帧。</summary>
    public int StartFrame => Mathf.Max(0, startFrame);

    /// <summary>目标节点 Id；空值表示停止。</summary>
    public string TargetNodeId => targetNodeId;

    /// <summary>同节点多个规则的匹配优先级。</summary>
    public int Priority => priority;
}

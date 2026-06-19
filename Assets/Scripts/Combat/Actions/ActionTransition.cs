using System;
using UnityEngine;

/// <summary>招式结束或事件触发时的自动衔接（与 CancelWindow 互补）。</summary>
[Serializable]
public class ActionTransition
{
    [SerializeField] ActionTransitionCondition condition = ActionTransitionCondition.AnimationEnd;
    [SerializeField] ActionDefinition targetAction;
    [SerializeField] int priority;

    public ActionTransitionCondition Condition => condition;
    public ActionDefinition TargetAction => targetAction;
    public int Priority => priority;
}

using System;
using UnityEngine;

/// <summary>招式结束或指定帧的自动衔接（与 CancelWindow 互补；Cancel 需输入，Transition 自动）。</summary>
[Serializable]
public class ActionTransition
{
    [SerializeField] ActionTransitionCondition condition = ActionTransitionCondition.AnimationEnd;
    [Tooltip("AtFrame：达到该帧（含）后触发；AnimationEnd 时忽略。")]
    [SerializeField] int startFrame;
    [SerializeField] ActionDefinition targetAction;
    [SerializeField] int priority;

    public ActionTransitionCondition Condition => condition;
    public int StartFrame => startFrame;
    public ActionDefinition TargetAction => targetAction;
    public int Priority => priority;
}

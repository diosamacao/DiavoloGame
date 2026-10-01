using System;
using UnityEngine;

/// <summary>动作自身固定的执行规则；不包含输入、选招、索敌或命中反馈策略。</summary>
[Serializable]
public sealed class ActionExecutionPolicy
{
    [Tooltip("动作打断优先级；更大则可硬打断更小者，同级不互相打断。")]
    [SerializeField] int interruptPriority = 0;

    [Tooltip("基础位移权威：None / BakedMotion / ScriptedTimeline / InputMovement（无 Animator RM）。")]
    [SerializeField] ActionBaseMotionMode baseMotionMode = ActionBaseMotionMode.None;

    /// <summary>当前动作是否独占输入移动。</summary>
    public bool UsesInputMovement => baseMotionMode == ActionBaseMotionMode.InputMovement;

    [Tooltip("StopOnContact：沿完整位移路径遇实体或墙停止；动画继续，不滑墙。")]
    [SerializeField] ActionBodyCollisionMode bodyCollisionMode = ActionBodyCollisionMode.SoftSeparationOnly;

    [Tooltip("角色接触额外间距（毫米）；不影响攻击盒。")]
    [Min(0)] [SerializeField] int bodyContactSkinMm = 20;

    /// <summary>连续动作位移的身体碰撞策略。</summary>
    public ActionBodyCollisionMode BodyCollisionMode => bodyCollisionMode;

    /// <summary>身体额外间距；负值由内容审计拒绝，不静默修正配置。</summary>
    public int BodyContactSkinMm => bodyContactSkinMm;

    /// <summary>用于构建与 Editor 共同校验碰撞作者配置。</summary>
    public bool HasValidBodyCollision => (bodyCollisionMode == ActionBodyCollisionMode.SoftSeparationOnly
        || bodyCollisionMode == ActionBodyCollisionMode.StopOnContact) && bodyContactSkinMm >= 0;

    /// <summary>动作打断优先级。</summary>
    public int InterruptPriority => interruptPriority;

    /// <summary>基础位移模式。</summary>
    public ActionBaseMotionMode BaseMotionMode => baseMotionMode;

    /// <summary>Editor 写回 BaseMotionMode；运行时不得调用。</summary>
    public void EditorSetBaseMotionMode(ActionBaseMotionMode mode) => baseMotionMode = mode;
}

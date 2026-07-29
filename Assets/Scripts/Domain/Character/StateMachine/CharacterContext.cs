using UnityEngine;

/// <summary>角色状态机共享上下文；State 只读取快照和服务引用，不直接查找场景对象。</summary>
public class CharacterContext
{
    LocomotionResumeRequest _pendingLocomotionResume;
    CharacterReactionRequest _pendingReaction;

    public CharacterContext(
        Transform transform,
        CharacterAnimationService animation,
        CharacterController motor,
        CharacterMotor movement)
    {
        Transform = transform;
        Animation = animation;
        Motor = motor;
        Movement = movement;
    }

    public Transform Transform { get; }
    public CharacterAnimationService Animation { get; }
    public CharacterController Motor { get; }
    public CharacterMotor Movement { get; }

    /// <summary>Locomotion 内层状态机；由工厂注入。</summary>
    public LocomotionStateMachine LocomotionStateMachine { get; set; }

    public float MoveInputMagnitude { get; set; }
    public float RunThreshold { get; set; }
    public bool IsGrounded { get; set; }
    public ICharacterStateMachine StateMachine { get; set; }

    /// <summary>单角色动作执行器，由 ActionState 推进。</summary>
    public IActionExecutor ActionExecutor { get; set; }

    /// <summary>动作状态下的转向服务。</summary>
    public ActionRotationDriver ActionRotation { get; set; }

    /// <summary>死亡表现是否已播放完成；供生命周期控制器决定何时回收。</summary>
    public bool DeathPresentationComplete { get; set; }

    /// <summary>写入 Action→Locomotion 的一次性恢复请求；后写入覆盖前请求。</summary>
    public void SetLocomotionResumeRequest(in LocomotionResumeRequest request)
    {
        _pendingLocomotionResume = request;
    }

    /// <summary>取出并清空一次性恢复请求，防止影响后续状态切换。</summary>
    public LocomotionResumeRequest ConsumeLocomotionResumeRequest()
    {
        LocomotionResumeRequest request = _pendingLocomotionResume;
        _pendingLocomotionResume = default;
        return request;
    }

    /// <summary>写入下一次 Hit/Death 状态要消费的表现请求。</summary>
    public void SetReactionRequest(in CharacterReactionRequest request)
    {
        _pendingReaction = request;
    }

    /// <summary>取出并清空当前反应请求，避免后续状态复用旧动作。</summary>
    public CharacterReactionRequest ConsumeReactionRequest()
    {
        CharacterReactionRequest request = _pendingReaction;
        _pendingReaction = default;
        return request;
    }
}

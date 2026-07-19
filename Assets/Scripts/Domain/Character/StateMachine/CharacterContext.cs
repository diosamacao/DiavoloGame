using UnityEngine;

/// <summary>角色状态机共享上下文；State 只读取快照和服务引用，不直接查找场景对象。</summary>
public class CharacterContext
{
    LocomotionResumeRequest _pendingLocomotionResume;

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

    /// <summary>Locomotion 相位服务；由工厂注入。</summary>
    public LocomotionService Locomotion { get; set; }

    public float MoveInputMagnitude { get; set; }
    public float RunThreshold { get; set; }
    public bool IsGrounded { get; set; }
    public ICharacterStateMachine StateMachine { get; set; }

    /// <summary>单角色动作执行器，由 ActionState 推进。</summary>
    public IActionExecutor ActionExecutor { get; set; }

    /// <summary>动作状态下的转向服务。</summary>
    public ActionRotationDriver ActionRotation { get; set; }

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
}

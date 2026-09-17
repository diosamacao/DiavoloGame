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

    /// <summary>单角色纯动作模拟核；推进由 CharacterActor 统一负责。</summary>
    public ActionSim ActionSim { get; set; }

    /// <summary>动作状态下的转向服务。</summary>
    public ActionRotationDriver ActionRotation { get; set; }

    /// <summary>死亡逻辑序列是否结束；Party 门禁以它为主信号，并由确定性帧上限兜底。</summary>
    public bool DeathSequenceComplete { get; set; }

    /// <summary>本次死亡 Action 的确定性总帧数；无死亡 Action 时为 0。</summary>
    public int DeathActionTotalFrames { get; set; }

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

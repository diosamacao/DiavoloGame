using UnityEngine;

/// <summary>单角色运行实例，集中持有输入、移动、状态、动作和战斗服务。</summary>
public sealed class CharacterActor
{
    readonly ICharacterInputSource _inputSource;
    readonly InputManager _inputManager;
    readonly CharacterMotor _motor;
    readonly CharacterStateMachine _stateMachine;
    readonly CharacterActionDriver _actionDriver;
    readonly CombatModeService _combatMode;

    /// <summary>角色输入中枢，玩家相机等系统可读取 LookIntent。</summary>
    public InputManager Input => _inputManager;

    /// <summary>当前移动输入幅度。</summary>
    public float MoveInputMagnitude => _motor.MoveInputMagnitude;

    /// <summary>当前跑步阈值。</summary>
    public float RunThreshold => _motor.RunThreshold;

    /// <summary>当前是否着地。</summary>
    public bool IsGrounded => _motor.IsGrounded;

    /// <summary>当前战斗模式服务。</summary>
    public ICombatModeService CombatMode => _combatMode;

    /// <summary>创建角色实例；所有依赖由工厂一次性注入。</summary>
    public CharacterActor(
        ICharacterInputSource inputSource,
        InputManager inputManager,
        CharacterMotor motor,
        CharacterStateMachine stateMachine,
        CharacterActionDriver actionDriver,
        CombatModeService combatMode)
    {
        _inputSource = inputSource;
        _inputManager = inputManager;
        _motor = motor;
        _stateMachine = stateMachine;
        _actionDriver = actionDriver;
        _combatMode = combatMode;
    }

    /// <summary>启用输入源。</summary>
    public void Enable() => _inputSource.Enable();

    /// <summary>禁用输入源。</summary>
    public void Disable() => _inputSource.Disable();

    /// <summary>更新相机 Transform，用于相机相对移动。</summary>
    public void SetCameraTransform(Transform cameraTransform)
    {
        _motor.SetCameraTransform(cameraTransform);
    }

    /// <summary>按固定顺序推进输入、动作路由、重力与状态机。</summary>
    public void Tick(float deltaTime)
    {
        _inputManager.IngestFrame(_inputSource.CaptureFrame());
        _actionDriver.ProcessGameplayInput();
        _motor.TickGravity(deltaTime);
        _stateMachine.Tick(deltaTime);
    }
}

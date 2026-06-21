using UnityEngine;

/// <summary>玩家角色纯 C# 运行时，集中持有输入、移动、状态、动作和战斗服务。</summary>
public sealed class PlayerCharacterRuntime : IActionStartContext, IMoveIntentResolver
{
    readonly Transform _root;
    readonly CharacterController _controller;
    readonly CharacterMotorConfig _motorConfig;
    readonly InputReader _inputReader;
    readonly InputManager _inputManager;
    readonly CharacterStateMachine _stateMachine;
    readonly CharacterActionDriver _actionDriver;
    ActionRotationDriver _rotationDriver;
    readonly CombatModeController _combatMode;

    Transform _cameraTransform;
    Vector3 _velocity;
    float _rotationVelocity;
    float _moveInputMagnitude;

    /// <summary>玩家输入中枢，供 CameraManager 等系统读取 LookIntent。</summary>
    public InputManager Input => _inputManager;

    /// <summary>当前移动输入幅度。</summary>
    public float MoveInputMagnitude => _moveInputMagnitude;

    /// <summary>当前跑步阈值。</summary>
    public float RunThreshold => _motorConfig.RunThreshold;

    /// <summary>当前是否着地。</summary>
    public bool IsGrounded => _controller.isGrounded;

    /// <summary>当前战斗模式控制器。</summary>
    public ICombatModeController CombatMode => _combatMode;

    public float DefaultRotationSmoothTime => _motorConfig.RotationSmoothTime;

    /// <summary>创建玩家运行时；所有依赖由工厂一次性注入。</summary>
    public PlayerCharacterRuntime(
        Transform root,
        CharacterController controller,
        CharacterMotorConfig motorConfig,
        InputReader inputReader,
        InputManager inputManager,
        CharacterStateMachine stateMachine,
        CharacterActionDriver actionDriver,
        CombatModeController combatMode,
        Transform cameraTransform)
    {
        _root = root;
        _controller = controller;
        _motorConfig = motorConfig;
        _inputReader = inputReader;
        _inputManager = inputManager;
        _stateMachine = stateMachine;
        _actionDriver = actionDriver;
        _combatMode = combatMode;
        _cameraTransform = cameraTransform;
    }

    /// <summary>绑定动作旋转服务；它依赖本 runtime 解析移动意图，因此构造后注入。</summary>
    public void BindRotationDriver(ActionRotationDriver rotationDriver)
    {
        _rotationDriver = rotationDriver;
    }

    /// <summary>启用输入资产。</summary>
    public void Enable() => _inputReader.Enable();

    /// <summary>禁用输入资产。</summary>
    public void Disable() => _inputReader.Disable();

    /// <summary>更新相机 Transform，用于相机相对移动。</summary>
    public void SetCameraTransform(Transform cameraTransform)
    {
        _cameraTransform = cameraTransform;
    }

    /// <summary>按固定顺序推进输入、移动、状态机与动作旋转。</summary>
    public void Tick(float deltaTime)
    {
        _inputManager.IngestFrame(_inputReader.CaptureFrame());
        _actionDriver.ProcessGameplayInput();
        ExecuteLocomotionMovement(deltaTime);
        ApplyGravity(deltaTime);
        _stateMachine.PushMotorSnapshot(_moveInputMagnitude, _motorConfig.RunThreshold, IsGrounded);
        _stateMachine.Tick(deltaTime);
        _rotationDriver?.Tick();
    }

    public void FaceBufferedMoveIntent()
    {
        Vector2 moveIntent = _inputManager.HasMoveIntent
            ? _inputManager.MoveIntent
            : _inputManager.BufferedMoveIntent;

        Vector3 direction = ResolveWorldMoveDirection(moveIntent);
        if (direction.sqrMagnitude < 0.001f)
            return;

        _root.rotation = Quaternion.LookRotation(direction);
    }

    public Vector3 ResolveWorldMoveDirection(Vector2 moveIntent)
    {
        if (moveIntent.sqrMagnitude < 0.01f)
            return Vector3.zero;

        if (_cameraTransform == null)
            return new Vector3(moveIntent.x, 0f, moveIntent.y).normalized;

        Vector3 forward = _cameraTransform.forward;
        Vector3 right = _cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return (forward * moveIntent.y + right * moveIntent.x).normalized;
    }

    /// <summary>Locomotion 状态下根据移动意图执行位移。</summary>
    void ExecuteLocomotionMovement(float deltaTime)
    {
        if (_stateMachine.CurrentStateId == CharacterStateType.Action)
        {
            _moveInputMagnitude = 0f;
            return;
        }

        Vector2 moveIntent = _inputManager.MoveIntent;
        Vector3 moveDirection = ResolveWorldMoveDirection(moveIntent);
        _moveInputMagnitude = _inputManager.MoveMagnitude;
        float speed = _moveInputMagnitude > _motorConfig.RunThreshold
            ? _motorConfig.RunSpeed
            : _motorConfig.WalkSpeed;

        if (moveDirection.sqrMagnitude <= 0.001f)
            return;

        _root.rotation = GetSmoothedRotation(moveDirection);
        _controller.Move(moveDirection * (speed * _moveInputMagnitude) * deltaTime);
    }

    Quaternion GetSmoothedRotation(Vector3 moveDirection)
    {
        if (_motorConfig.RotationSmoothTime <= 0.001f)
            return Quaternion.LookRotation(moveDirection);

        float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
        float angle = Mathf.SmoothDampAngle(
            _root.eulerAngles.y,
            targetAngle,
            ref _rotationVelocity,
            _motorConfig.RotationSmoothTime);

        return Quaternion.Euler(0f, angle, 0f);
    }

    void ApplyGravity(float deltaTime)
    {
        if (_controller.isGrounded && _velocity.y < 0f)
            _velocity.y = _motorConfig.GroundedGravity;

        _velocity.y += _motorConfig.Gravity * deltaTime;
        _controller.Move(_velocity * deltaTime);
    }
}

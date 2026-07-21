using UnityEngine;

/// <summary>角色移动服务：负责 Locomotion 位移、重力和移动意图到世界方向的解析。</summary>
public sealed class CharacterMotor : IActionStartContext, IMoveIntentResolver
{
    readonly Transform _root;
    readonly CharacterController _controller;
    readonly CharacterMotorConfig _config;
    readonly InputManager _input;

    Transform _cameraTransform;
    Vector3 _velocity;
    float _rotationVelocity;
    float _moveInputMagnitude;
    float _planarSpeedEstimate;

    /// <summary>创建角色移动服务；由状态机决定何时调用 Locomotion 移动。</summary>
    public CharacterMotor(
        Transform root,
        CharacterController controller,
        CharacterMotorConfig config,
        InputManager input,
        Transform cameraTransform)
    {
        _root = root;
        _controller = controller;
        _config = config;
        _input = input;
        _cameraTransform = cameraTransform;
    }

    /// <summary>当前移动输入幅度。</summary>
    public float MoveInputMagnitude => _moveInputMagnitude;

    /// <summary>跑步阈值。</summary>
    public float RunThreshold => _config.RunThreshold;

    /// <summary>跑速配置，供急停速度门槛计算。</summary>
    public float RunSpeed => _config.RunSpeed;

    /// <summary>冲刺速度配置。</summary>
    public float SprintSpeed => _config.SprintSpeed;

    /// <summary>当前是否着地。</summary>
    public bool IsGrounded => _controller.isGrounded;

    /// <summary>上一帧水平位移估算速度（m/s）。</summary>
    public float PlanarSpeedEstimate => _planarSpeedEstimate;

    public float DefaultRotationSmoothTime => _config.RotationSmoothTime;

    /// <summary>更新相机 Transform，用于相机相对移动。</summary>
    public void SetCameraTransform(Transform cameraTransform)
    {
        _cameraTransform = cameraTransform;
    }

    /// <summary>按 LocomotionService 命令执行水平位移与旋转（首版无加减速/转身专用位移）。</summary>
    public void ApplyLocomotion(in LocomotionMotorCommand command, float deltaTime)
    {
        Vector2 moveIntent = _input.MoveIntent;
        Vector3 moveDirection = ResolveWorldMoveDirection(moveIntent);
        _moveInputMagnitude = _input.MoveMagnitude;

        ApplyRotation(command, moveDirection);

        if (!command.ApplyHorizontalMove || moveDirection.sqrMagnitude <= 0.001f)
        {
            // 本帧快照已在 Apply 前采样上一帧速度；此处清零供后续帧使用。
            _planarSpeedEstimate = 0f;
            return;
        }

        float speed = ResolveSpeed(command.Gait);
        float planarSpeed = speed * _moveInputMagnitude;
        _planarSpeedEstimate = planarSpeed;
        _controller.Move(moveDirection * (planarSpeed * deltaTime));
    }

    float ResolveSpeed(LocomotionGait gait)
    {
        switch (gait)
        {
            case LocomotionGait.Sprint:
                return _config.SprintSpeed;
            case LocomotionGait.Run:
                return _config.RunSpeed;
            default:
                return _config.WalkSpeed;
        }
    }

    /// <summary>非 Locomotion 状态下清空移动幅度，避免动画继续判定为移动。</summary>
    public void ClearMoveSnapshot()
    {
        _moveInputMagnitude = 0f;
        _planarSpeedEstimate = 0f;
    }

    /// <summary>按命令选择输入跟随或显式 Pivot 目标，并共用同一套平滑旋转状态。</summary>
    void ApplyRotation(in LocomotionMotorCommand command, Vector3 moveDirection)
    {
        switch (command.RotationMode)
        {
            case LocomotionRotationMode.FollowInput:
                if (moveDirection.sqrMagnitude > 0.001f)
                    _root.rotation = GetSmoothedRotation(moveDirection, command.RotationSmoothTimeOverride);
                break;
            case LocomotionRotationMode.PivotTarget:
                if (command.PivotTargetDirection.sqrMagnitude > 0.001f)
                {
                    if (command.RotationSmoothTimeOverride.HasValue)
                    {
                        _root.rotation = GetSmoothedRotation(
                            command.PivotTargetDirection,
                            command.RotationSmoothTimeOverride);
                    }
                    else
                    {
                        FaceWorldDirection(command.PivotTargetDirection);
                    }
                }
                break;
            default:
                break;
        }
    }

    /// <summary>每帧应用重力；不属于某个 State，保持和物理执行同步。</summary>
    public void TickGravity(float deltaTime)
    {
        if (_controller.isGrounded && _velocity.y < 0f)
            _velocity.y = _config.GroundedGravity;

        _velocity.y += _config.Gravity * deltaTime;
        _controller.Move(_velocity * deltaTime);
    }

    /// <summary>按当前或缓冲移动输入朝向；无有效输入时保持原朝向。</summary>
    public void FaceBufferedMoveIntent()
    {
        if (!TryGetDodgeIntentDirection(out Vector3 direction))
            return;

        FaceWorldDirection(direction);
    }

    /// <summary>读取 Dodge 方向判定输入：优先当前输入，其次缓冲输入。</summary>
    public bool TryGetDodgeIntentDirection(out Vector3 direction)
    {
        Vector2 moveIntent = _input.HasMoveIntent
            ? _input.MoveIntent
            : _input.BufferedMoveIntent;

        direction = ResolveWorldMoveDirection(moveIntent);
        return direction.sqrMagnitude >= 0.001f;
    }

    /// <summary>按世界方向立即旋转角色；忽略 y 分量与极小向量，并清空转向阻尼避免回弹。</summary>
    public void FaceWorldDirection(Vector3 direction)
    {
        if (!TryNormalizePlanar(direction, out Vector3 normalizedDirection))
            return;

        _root.rotation = Quaternion.LookRotation(normalizedDirection);
        _rotationVelocity = 0f;
    }

    /// <summary>清空 SmoothDamp 转向速度；Pivot 进出时调用，防止结束后朝向回摆。</summary>
    public void ResetRotationDamping() => _rotationVelocity = 0f;

    /// <summary>应用世界平面位移（烘焙根运动）；不改朝向。</summary>
    public void MovePlanar(Vector3 worldDelta, float deltaTime)
    {
        worldDelta.y = 0f;
        if (worldDelta.sqrMagnitude < 0.0000001f)
            return;

        _controller.Move(worldDelta);
        if (deltaTime > 0.0001f)
            _planarSpeedEstimate = worldDelta.magnitude / deltaTime;
    }

    /// <summary>绕 Y 叠加偏航（烘焙根旋转）。</summary>
    public void ApplyYawDegrees(float yawDeltaDegrees)
    {
        if (Mathf.Abs(yawDeltaDegrees) < 0.0001f)
            return;

        _root.rotation = Quaternion.Euler(0f, yawDeltaDegrees, 0f) * _root.rotation;
        _rotationVelocity = 0f;
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

    /// <summary>按目标方向 SmoothDamp 转向；可覆盖平滑时间（Pivot 跟 demo ReturnRun 用更长阻尼）。</summary>
    Quaternion GetSmoothedRotation(Vector3 moveDirection, float? smoothTimeOverride = null)
    {
        float smoothTime = smoothTimeOverride ?? _config.RotationSmoothTime;
        if (smoothTime <= 0.001f)
            return Quaternion.LookRotation(moveDirection);

        float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
        float angle = Mathf.SmoothDampAngle(
            _root.eulerAngles.y,
            targetAngle,
            ref _rotationVelocity,
            smoothTime);

        return Quaternion.Euler(0f, angle, 0f);
    }

    /// <summary>将向量投影到 XZ 平面并单位化；长度过小时返回 false。</summary>
    static bool TryNormalizePlanar(Vector3 source, out Vector3 normalized)
    {
        source.y = 0f;
        if (source.sqrMagnitude < 0.0001f)
        {
            normalized = Vector3.zero;
            return false;
        }

        normalized = source.normalized;
        return true;
    }
}

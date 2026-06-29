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

    /// <summary>当前是否着地。</summary>
    public bool IsGrounded => _controller.isGrounded;

    public float DefaultRotationSmoothTime => _config.RotationSmoothTime;

    /// <summary>更新相机 Transform，用于相机相对移动。</summary>
    public void SetCameraTransform(Transform cameraTransform)
    {
        _cameraTransform = cameraTransform;
    }

    /// <summary>Locomotion 状态下根据移动意图执行水平位移。</summary>
    public void TickLocomotion(float deltaTime)
    {
        Vector2 moveIntent = _input.MoveIntent;
        Vector3 moveDirection = ResolveWorldMoveDirection(moveIntent);
        _moveInputMagnitude = _input.MoveMagnitude;
        float speed = _moveInputMagnitude > _config.RunThreshold
            ? _config.RunSpeed
            : _config.WalkSpeed;

        if (moveDirection.sqrMagnitude <= 0.001f)
            return;

        _root.rotation = GetSmoothedRotation(moveDirection);
        _controller.Move(moveDirection * (speed * _moveInputMagnitude) * deltaTime);
    }

    /// <summary>非 Locomotion 状态下清空移动幅度，避免动画继续判定为移动。</summary>
    public void ClearMoveSnapshot()
    {
        _moveInputMagnitude = 0f;
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

    /// <summary>按世界方向立即旋转角色；忽略 y 分量与极小向量。</summary>
    public void FaceWorldDirection(Vector3 direction)
    {
        if (!TryNormalizePlanar(direction, out Vector3 normalizedDirection))
            return;

        _root.rotation = Quaternion.LookRotation(normalizedDirection);
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

    Quaternion GetSmoothedRotation(Vector3 moveDirection)
    {
        if (_config.RotationSmoothTime <= 0.001f)
            return Quaternion.LookRotation(moveDirection);

        float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
        float angle = Mathf.SmoothDampAngle(
            _root.eulerAngles.y,
            targetAngle,
            ref _rotationVelocity,
            _config.RotationSmoothTime);

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

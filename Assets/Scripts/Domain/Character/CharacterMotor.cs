using UnityEngine;

/// <summary>
/// 角色移动服务：水平与竖直权威均在 CharacterMotorSim；Transform/CC 只跟随表现，不再 Move。
/// </summary>
public sealed class CharacterMotor : IActionStartContext, IMoveIntentResolver
{
    readonly Transform _root;
    readonly CharacterController _controller;
    readonly CharacterMotorConfig _config;
    readonly InputManager _input;
    readonly CharacterMotorSim _sim;

    Transform _cameraTransform;
    float _rotationVelocity;
    float _moveInputMagnitude;
    float _planarSpeedEstimate;

    /// <summary>创建角色移动服务；由状态机决定何时调用 Locomotion 移动。</summary>
    public CharacterMotor(
        Transform root,
        CharacterController controller,
        CharacterMotorConfig config,
        InputManager input,
        Transform cameraTransform,
        CharacterMotorSim motorSim = null)
    {
        _root = root;
        _controller = controller;
        _config = config;
        _input = input;
        _cameraTransform = cameraTransform;
        _sim = motorSim ?? new CharacterMotorSim(
            OpenFieldSimCollisionWorld.Instance,
            MotionQuantization.MetersToMm(config.ControllerRadius),
            config.SoftBodyMass,
            config.SoftBodyImmovable,
            SimulationConfig.DefaultLogicHz,
            MotionQuantization.MetersToMm(config.Gravity),
            MotionQuantization.MetersToMm(config.GroundedGravity));
        CapturePoseFromRoot();
    }

    /// <summary>逻辑电机；位移与着地权威源。</summary>
    public CharacterMotorSim Sim => _sim;

    /// <summary>当前移动输入幅度。</summary>
    public float MoveInputMagnitude => _moveInputMagnitude;

    /// <summary>跑步阈值。</summary>
    public float RunThreshold => _config.RunThreshold;

    /// <summary>跑速配置，供急停速度门槛计算。</summary>
    public float RunSpeed => _config.RunSpeed;

    /// <summary>冲刺速度配置。</summary>
    public float SprintSpeed => _config.SprintSpeed;

    /// <summary>当前是否着地（MotorSim 权威）。</summary>
    public bool IsGrounded => _sim.IsGrounded;

    /// <summary>上一帧水平位移估算速度（m/s）。</summary>
    public float PlanarSpeedEstimate => _planarSpeedEstimate;

    public float DefaultRotationSmoothTime => _config.RotationSmoothTime;

    /// <summary>更新相机 Transform，用于相机相对移动。</summary>
    public void SetCameraTransform(Transform cameraTransform)
    {
        _cameraTransform = cameraTransform;
    }

    /// <summary>从当前 Unity 根对齐逻辑位姿（生成/传送后调用）。</summary>
    public void CapturePoseFromRoot()
    {
        Vector3 p = _root.position;
        _sim.TeleportMeters(p.x, p.y, p.z);
        _sim.SetFacingDegrees(_root.eulerAngles.y);
    }

    /// <summary>按 Locomotion 内层状态命令执行水平位移与旋转（首版无加减速/转身专用位移）。</summary>
    public void ApplyLocomotion(in LocomotionMotorCommand command, float deltaTime)
    {
        Vector2 moveIntent = _input.MoveIntent;
        Vector3 moveDirection = ResolveWorldMoveDirection(moveIntent);
        _moveInputMagnitude = _input.MoveMagnitude;

        ApplyRotation(command, moveDirection, deltaTime);

        if (!command.ApplyHorizontalMove || moveDirection.sqrMagnitude <= 0.001f)
        {
            // 本帧快照已在 Apply 前采样上一帧速度；此处清零供后续帧使用。
            _planarSpeedEstimate = 0f;
            return;
        }

        float speed = ResolveSpeed(command.Gait);
        float planarSpeed = speed * _moveInputMagnitude;
        _planarSpeedEstimate = planarSpeed;
        Vector3 worldDelta = moveDirection * (planarSpeed * deltaTime);
        MovePlanar(worldDelta, deltaTime);
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
    void ApplyRotation(
        in LocomotionMotorCommand command,
        Vector3 moveDirection,
        float deltaTime)
    {
        switch (command.RotationMode)
        {
            case LocomotionRotationMode.FollowInput:
                if (moveDirection.sqrMagnitude > 0.001f)
                {
                    _root.rotation = GetSmoothedRotation(
                        moveDirection,
                        deltaTime,
                        command.RotationSmoothTimeOverride);
                    SyncFacingFromRoot();
                }
                break;
            case LocomotionRotationMode.PivotTarget:
                if (command.PivotTargetDirection.sqrMagnitude > 0.001f)
                {
                    if (command.RotationSmoothTimeOverride.HasValue)
                    {
                        _root.rotation = GetSmoothedRotation(
                            command.PivotTargetDirection,
                            deltaTime,
                            command.RotationSmoothTimeOverride);
                        SyncFacingFromRoot();
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

    /// <summary>每逻辑帧推进 MotorSim 重力/着地，并把完整 XYZ 写回根；不再调用 CC.Move。</summary>
    public void TickGravity(float deltaTime)
    {
        _ = deltaTime;
        _sim.TickVertical();
        SyncRootFromSim();
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
        SyncFacingFromRoot();
    }

    /// <summary>清空 SmoothDamp 转向速度；Pivot 进出时调用，防止结束后朝向回摆。</summary>
    public void ResetRotationDamping() => _rotationVelocity = 0f;

    /// <summary>应用世界平面位移（烘焙/脚本根运动）；权威写入 MotorSim 后再同步 Transform。</summary>
    public void MovePlanar(Vector3 worldDelta, float deltaTime)
    {
        worldDelta.y = 0f;
        if (worldDelta.sqrMagnitude < 0.0000001f)
            return;

        if (_sim.TryMoveWorldMeters(worldDelta.x, worldDelta.z))
            SyncRootFromSim();

        if (deltaTime > 0.0001f)
            _planarSpeedEstimate = worldDelta.magnitude / deltaTime;
    }

    /// <summary>按角色本地毫米 Δ（右/前）移动；供动作烘焙表使用。</summary>
    public void MoveLocalMm(SimVec2 localDeltaMm)
    {
        // 本地→世界依赖 Sim 朝向；先与 Transform 对齐，避免表现旋转未回写
        SyncFacingFromRoot();
        if (_sim.TryMoveLocalMm(localDeltaMm.X, localDeltaMm.Z))
            SyncRootFromSim();
    }

    /// <summary>绕 Y 叠加偏航（烘焙根旋转）。</summary>
    public void ApplyYawDegrees(float yawDeltaDegrees)
    {
        if (Mathf.Abs(yawDeltaDegrees) < 0.0001f)
            return;

        _root.rotation = Quaternion.Euler(0f, yawDeltaDegrees, 0f) * _root.rotation;
        _rotationVelocity = 0f;
        SyncFacingFromRoot();
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

    /// <summary>把 MotorSim 水平坐标写回角色根；软弹开后与位移路径共用。</summary>
    public void SyncRootPlanarFromSim() => SyncRootFromSim();

    /// <summary>
    /// 把 MotorSim 完整 XYZ 写回角色根。
    /// CC 必须保持禁用：重新 enable 时 PhysX 会把胶囊从地面挤出（约 center.y），表现层会采到悬空假高度。
    /// </summary>
    public void SyncRootFromSim()
    {
        Vector3 p = _root.position;
        p.x = MotionQuantization.MmToMeters(_sim.PositionMm.X);
        p.y = MotionQuantization.MmToMeters(_sim.YMm);
        p.z = MotionQuantization.MmToMeters(_sim.PositionMm.Z);

        // 逻辑位移/重力已不走 CC.Move；禁用后直接写 Transform，避免 enable 挤出地面
        if (_controller != null && _controller.enabled)
            _controller.enabled = false;
        _root.position = p;
    }

    /// <summary>把 Transform 偏航同步进 MotorSim，供本地表位移旋转。</summary>
    void SyncFacingFromRoot() => _sim.SetFacingDegrees(_root.eulerAngles.y);

    /// <summary>按目标方向和显式固定步长 SmoothDamp 转向；不读取 Unity Time.deltaTime。</summary>
    Quaternion GetSmoothedRotation(
        Vector3 moveDirection,
        float deltaTime,
        float? smoothTimeOverride = null)
    {
        float smoothTime = smoothTimeOverride ?? _config.RotationSmoothTime;
        if (smoothTime <= 0.001f)
            return Quaternion.LookRotation(moveDirection);

        float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
        float angle = Mathf.SmoothDampAngle(
            _root.eulerAngles.y,
            targetAngle,
            ref _rotationVelocity,
            smoothTime,
            Mathf.Infinity,
            Mathf.Max(0f, deltaTime));

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

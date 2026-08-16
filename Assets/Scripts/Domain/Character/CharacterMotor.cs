using UnityEngine;

/// <summary>
/// 角色移动服务：水平与竖直权威均在 CharacterMotorSim；Transform/CC 只跟随表现，不再 Move。
/// </summary>
public sealed class CharacterMotor : IActionStartContext, IMoveIntentResolver
{
    readonly Transform _root;
    readonly CharacterController _controller;
    readonly CharacterMotorConfig _config;
    readonly IMoveIntentSource _moveIntent;
    readonly CharacterMotorSim _sim;

    Vector3 _faceTargetForward;
    bool _hasFaceTargetForward;
    float _rotationVelocity;
    float _moveInputMagnitude;
    float _planarSpeedEstimate;
    /// <summary>逻辑帧采样的 wish（供调试箭头）；避免 LateUpdate 跟相机基重算导致抖动。</summary>
    Vector3 _debugWishWorldDirection;

    /// <summary>创建角色移动服务；由状态机决定何时调用 Locomotion 移动。</summary>
    public CharacterMotor(
        Transform root,
        CharacterController controller,
        CharacterMotorConfig config,
        IMoveIntentSource moveIntent,
        CharacterMotorSim motorSim = null)
    {
        _root = root;
        _controller = controller;
        _config = config;
        _moveIntent = moveIntent;
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

    /// <summary>最近一次逻辑帧解析的世界 wish；调试可视化专用，渲染帧勿重算。</summary>
    public Vector3 DebugWishWorldDirection => _debugWishWorldDirection;

    /// <summary>写入 FaceTarget 水平朝向；由 Locomotion 每逻辑帧发布。</summary>
    public void SetFaceTargetWorldDirection(Vector3 planarForward)
    {
        planarForward.y = 0f;
        if (planarForward.sqrMagnitude < 0.0001f)
        {
            _hasFaceTargetForward = false;
            return;
        }

        _faceTargetForward = planarForward.normalized;
        _hasFaceTargetForward = true;
    }

    /// <summary>清除 FaceTarget 朝向。</summary>
    public void ClearFaceTargetWorldDirection() => _hasFaceTargetForward = false;

    /// <summary>从当前 Unity 根对齐逻辑位姿（生成/传送后调用）。</summary>
    public void CapturePoseFromRoot()
    {
        Vector3 p = _root.position;
        _sim.TeleportMeters(p.x, p.y, p.z);
        _sim.SetFacingDegrees(_root.eulerAngles.y);
    }

    /// <summary>
    /// 按 Locomotion 命令执行水平位移与旋转。
    /// FollowInput：朝向以 RotationSmoothTime 追 wish，位移沿更新后的朝向——只调一个平滑时间即可拉长 W→WD 转向。
    /// FaceCamera / FaceTarget 位移沿 wish（strafing）。
    /// </summary>
    public void ApplyLocomotion(in LocomotionMotorCommand command, float deltaTime)
    {
        Vector2 moveIntent = _moveIntent.MoveIntent;
        Vector3 wishDirection = ResolveWorldMoveDirection(moveIntent);
        CaptureDebugWishWorldDirection(wishDirection);
        _moveInputMagnitude = _moveIntent.MoveMagnitude;

        // 先转朝向，再取 forward 作为 FollowInput 位移，使同帧平滑生效
        ApplyRotation(command, wishDirection, deltaTime);

        Vector3 moveDirection = ResolveLocomotionMoveDirection(command.RotationMode, wishDirection);
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

    /// <summary>
    /// FollowInput 位移锁当前朝向（与转向共用 RotationSmoothTime）；其余模式位移锁 wish。
    /// </summary>
    Vector3 ResolveLocomotionMoveDirection(LocomotionRotationMode rotationMode, Vector3 wishDirection)
    {
        if (wishDirection.sqrMagnitude <= 0.001f)
            return Vector3.zero;

        if (rotationMode != LocomotionRotationMode.FollowInput)
            return wishDirection;

        Vector3 facing = _root.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f)
            return wishDirection;
        return facing.normalized;
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

    /// <summary>按命令选择 FollowInput / FaceCamera / Hold，并共用同一套平滑旋转状态。</summary>
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
            case LocomotionRotationMode.FaceCamera:
                // 八向：位移可横移，偏航锁相机/假相机水平前向
                Vector3 cameraForward = ResolveMoveReferencePlanarForward();
                if (cameraForward.sqrMagnitude > 0.001f)
                {
                    _root.rotation = GetSmoothedRotation(
                        cameraForward,
                        deltaTime,
                        command.RotationSmoothTimeOverride);
                    SyncFacingFromRoot();
                }
                break;
            case LocomotionRotationMode.FaceTarget:
                // 锁定 strafing：偏航锁目标，位移沿 wish
                if (_hasFaceTargetForward)
                {
                    _root.rotation = GetSmoothedRotation(
                        _faceTargetForward,
                        deltaTime,
                        command.RotationSmoothTimeOverride);
                    SyncFacingFromRoot();
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
        // 竖直积分与着地在 MotorSim；deltaTime 仅占位，步长由 World 固定
        _sim.TickVertical();
        // 完整 XYZ 写回根 Transform，禁止 CC.Move
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
        Vector2 moveIntent = _moveIntent.HasMoveIntent
            ? _moveIntent.MoveIntent
            : _moveIntent.BufferedMoveIntent;

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

    /// <summary>按世界水平毫米 Δ 移动；供 TargetAdhesion 修正使用。</summary>
    public void MoveWorldMm(int dxMm, int dzMm)
    {
        if (dxMm == 0 && dzMm == 0)
            return;

        if (_sim.TryMoveWorldMm(dxMm, dzMm))
            SyncRootFromSim();
    }

    /// <summary>
    /// MotionCommand 提交后：把 MotorSim 位置与朝向同步到角色根。
    /// Resolver 已写入 Sim；此处只负责表现根对齐。
    /// </summary>
    public void SyncRootPoseFromSim()
    {
        SyncRootFromSim();
        float yaw = MotionQuantization.MilliDegToDegrees(_sim.FacingMilliDeg);
        _root.rotation = Quaternion.Euler(0f, yaw, 0f);
        _rotationVelocity = 0f;
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

    /// <summary>按量化 MoveReferenceYaw 把本地移动轴解析为世界水平 wish。</summary>
    public Vector3 ResolveWorldMoveDirection(Vector2 moveIntent)
    {
        if (moveIntent.sqrMagnitude < 0.01f)
            return Vector3.zero;

        float yaw = InputQuantizer.DequantizeYaw(_moveIntent.MoveReferenceYawQuantized);
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = rotation * Vector3.forward;
        Vector3 right = rotation * Vector3.right;
        return (forward * moveIntent.y + right * moveIntent.x).normalized;
    }

    /// <summary>写入逻辑帧 wish 快照；Locomotion 快照与 ApplyLocomotion 共用。</summary>
    public void CaptureDebugWishWorldDirection(Vector3 wishWorld)
    {
        wishWorld.y = 0f;
        _debugWishWorldDirection = wishWorld.sqrMagnitude > 0.0001f
            ? wishWorld.normalized
            : Vector3.zero;
    }

    /// <summary>返回 InputFrame/AI Desire 固化的世界移动参考前向。</summary>
    public Vector3 ResolveMoveReferencePlanarForward()
    {
        float yaw = InputQuantizer.DequantizeYaw(_moveIntent.MoveReferenceYawQuantized);
        return Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
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

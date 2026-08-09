using UnityEngine;

/// <summary>
/// 内层 Locomotion 状态机共享上下文：依赖、跨相位数据与帧快照；
/// 相位决策留在各 State，此处只提供执行辅助。
/// </summary>
public sealed class LocomotionContext
{
    LocomotionStateMachine _machine;

    /// <summary>创建上下文并注入运行时依赖。</summary>
    public LocomotionContext(
        Transform root,
        CharacterMotor motor,
        CharacterAnimationService animation,
        InputManager input,
        CharacterLocomotionProfile profile,
        LocomotionFootCycle footCycle,
        LocomotionFootstepPlayer footstepPlayer,
        LocomotionRootMotionPlayer rootMotionPlayer,
        ILocomotionAnimResolver animResolver = null)
    {
        Root = root;
        Motor = motor;
        Animation = animation;
        Input = input;
        Profile = profile;
        FootCycle = footCycle;
        FootstepPlayer = footstepPlayer;
        RootMotionPlayer = rootMotionPlayer;
        AnimResolver = animResolver ?? new DefaultLocomotionAnimResolver();
    }

    public Transform Root { get; }
    public CharacterMotor Motor { get; }
    public CharacterAnimationService Animation { get; }
    public InputManager Input { get; }
    public CharacterLocomotionProfile Profile { get; }
    public LocomotionFootCycle FootCycle { get; }
    public LocomotionFootstepPlayer FootstepPlayer { get; }
    public LocomotionRootMotionPlayer RootMotionPlayer { get; }

    /// <summary>步态+局部输入 → AnimationKey。</summary>
    public ILocomotionAnimResolver AnimResolver { get; }

    /// <summary>当前稳态步态（Gait 相位内可变）。</summary>
    public LocomotionGait Gait { get; set; } = LocomotionGait.Walk;

    /// <summary>切入 Gait 前写入的目标步态。</summary>
    public LocomotionGait PendingGait { get; set; } = LocomotionGait.Walk;

    /// <summary>Gait.Enter 时硬切并立即 Play 目标步态 Clip（Resume / Pivot 结束）。</summary>
    public bool PendingGaitHardCutPlay { get; set; }

    /// <summary>Gait.Enter 后可选对齐的朝向；零向量表示跳过。</summary>
    public Vector3 PendingGaitFaceDirection { get; set; }

    /// <summary>进入 TurnBack 时的输入目标；解锁后用相对偏移修正角色根。</summary>
    public Vector3 PivotInitialTargetDirection { get; set; } = Vector3.forward;

    /// <summary>Pivot 期间持续刷新的输入目标朝向。</summary>
    public Vector3 PivotTargetDirection { get; set; } = Vector3.forward;

    /// <summary>进入 Pivot 瞬间的根朝向；锁根与烘焙位移基。</summary>
    public Vector3 PivotEnterFacing { get; set; } = Vector3.forward;

    /// <summary>TurnBack 已持续秒数；达解锁时间后允许输入偏移。</summary>
    public float PivotElapsedSeconds { get; set; }

    /// <summary>本次 Pivot 是否出现过移动输入；结束时用于直入 Sprint。</summary>
    public bool PivotMoveLatched { get; set; }

    /// <summary>进入 Stop 时的朝向；烘焙根位移局部→世界基。</summary>
    public Vector3 StopEnterFacing { get; set; } = Vector3.forward;

    /// <summary>当前急停使用的 AnimationKey（StartEnd / StopL / StopR）。</summary>
    public AnimationKey StopKey { get; set; } = AnimationKey.StopR;

    /// <summary>本次 Start 相位锁定的起步 Key（WalkStartLeft/Right / WalkStart / Start）。</summary>
    public AnimationKey ActiveStartKey { get; set; } = AnimationKey.Start;

    /// <summary>切入 Stop 前是否来自 Start（决定 StartEnd）。</summary>
    public bool StopFromStart { get; set; }

    /// <summary>Stop.Enter 以 0 fade 硬切（Pivot 结束接急停）。</summary>
    public bool StopPlayHardCut { get; set; }

    /// <summary>Run 满输入累计时长；进 Sprint 或离开跑档时清零。</summary>
    public float RunHoldSeconds { get; set; }

    /// <summary>Gait 下连续无移动输入累计；未超宽限不进 Stop。</summary>
    public float GaitInputGapSeconds { get; set; }

    /// <summary>本帧输入/运动学快照；由 StateMachine.Tick 前填充。</summary>
    public LocomotionInputSnapshot FrameSnapshot { get; set; }

    /// <summary>本帧 deltaTime。</summary>
    public float DeltaTime { get; set; }

    public bool LoggedMissingStart { get; set; }
    public bool LoggedMissingPivot { get; set; }
    public bool LoggedMissingStop { get; set; }
    public bool LoggedMissingStartEnd { get; set; }
    public bool LoggedMissingSprint { get; set; }

    /// <summary>绑定所属状态机，供各态 RequestPhase。</summary>
    public void BindMachine(LocomotionStateMachine machine) => _machine = machine;

    /// <summary>请求切换内层相位；默认全开，由调用方决定时机。</summary>
    public bool RequestPhase(LocomotionPhase next, bool force = false) =>
        _machine != null && _machine.TryChangePhase(next, force);

    /// <summary>构建本帧输入快照。</summary>
    public LocomotionInputSnapshot BuildSnapshot()
    {
        Vector2 moveIntent = Input.MoveIntent;
        float magnitude = Input.MoveMagnitude;
        bool hasMove = Input.HasMoveIntent;
        Vector3 worldDir = Motor.ResolveWorldMoveDirection(moveIntent);
        return new LocomotionInputSnapshot(
            moveIntent,
            magnitude,
            hasMove,
            worldDir,
            Motor.IsGrounded,
            Motor.PlanarSpeedEstimate);
    }

    /// <summary>是否达到 Idle 判定阈值以上的有效移动输入。</summary>
    public bool HasMeaningfulMove(in LocomotionInputSnapshot snapshot)
    {
        float idleThreshold = Profile != null ? Profile.IdleInputThreshold : 0.01f;
        return snapshot.HasMoveInput && snapshot.Magnitude >= idleThreshold;
    }

    /// <summary>起步结束时的初始步态：Walk/Run，并受 GaitPolicy.MaxGait 钳制。</summary>
    public LocomotionGait ResolveInitialGait(float magnitude)
    {
        LocomotionGait gait = magnitude > Motor.RunThreshold
            ? LocomotionGait.Run
            : LocomotionGait.Walk;
        LocomotionGaitPolicy policy = Profile != null ? Profile.GaitPolicy : null;
        return policy != null ? policy.ClampGait(gait) : gait;
    }

    /// <summary>按初始步态 + 本地输入解析起步 Clip；结果写入 ActiveStartKey。</summary>
    public AnimationKey ResolveAndLatchStartKey(float magnitude, Vector2 localMoveIntent)
    {
        LocomotionGait gait = ResolveInitialGait(magnitude);
        ActiveStartKey = DefaultLocomotionAnimResolver.ResolveStartKey(
            gait,
            localMoveIntent,
            Animation);
        return ActiveStartKey;
    }

    /// <summary>Start/Gait 移动时的旋转模式（来自 Profile，默认 FollowInput）。</summary>
    public LocomotionRotationMode ResolveGaitRotationMode() =>
        Profile != null ? Profile.GaitRotationMode : LocomotionRotationMode.FollowInput;

    /// <summary>Run / Sprint 视为跑档急停门槛。</summary>
    public static bool IsRunTier(LocomotionGait gait) =>
        gait == LocomotionGait.Run || gait == LocomotionGait.Sprint;

    /// <summary>经 AnimResolver 解析本帧 Locomotion 动画键（含 WalkLeft/Right）。</summary>
    public AnimationKey ResolveLocomotionAnimationKey()
    {
        LocomotionGait gait = Gait;
        Vector2 localMove = FrameSnapshot.MoveIntent;
        AnimationKey key = AnimResolver.Resolve(gait, localMove, Animation);

        // Sprint 缺 Clip：首次告警后 Resolver 已回退 Run
        if (gait == LocomotionGait.Sprint
            && key == AnimationKey.Run
            && !Animation.HasClip(AnimationKey.Sprint)
            && !LoggedMissingSprint)
        {
            Debug.LogError("LocomotionStateMachine: AnimationProfile 未绑定 Sprint Clip，暂用 Run。");
            LoggedMissingSprint = true;
        }

        return key;
    }

    /// <summary>按当前相位/步态取落脚标记。</summary>
    public FootPlantMarker[] GetMarkersForPhase(LocomotionPhase phase)
    {
        if (Profile == null)
            return System.Array.Empty<FootPlantMarker>();
        if (phase == LocomotionPhase.Start)
            return Profile.StartFootPlants;
        if (phase == LocomotionPhase.Gait)
            return Profile.GetGaitFootPlants(Gait);
        return System.Array.Empty<FootPlantMarker>();
    }

    /// <summary>写入步态并刷新 Gait 落脚标记。</summary>
    public void SetGait(LocomotionGait gait)
    {
        Gait = gait;
        FootCycle.SetMarkers(GetMarkersForPhase(LocomotionPhase.Gait));
    }

    /// <summary>
    /// 解析急停 Clip 并写入 StopKey。Start→StartEnd；否则按落脚 StopL/R。
    /// 失败时返回 false（调用方应进 Idle）。
    /// </summary>
    public bool TryResolveStopKey(bool fromStart)
    {
        if (fromStart && Animation.HasClip(AnimationKey.StartEnd))
        {
            StopKey = AnimationKey.StartEnd;
            return true;
        }

        if (fromStart && !LoggedMissingStartEnd)
        {
            Debug.LogError("LocomotionStateMachine: AnimationProfile 未绑定 StartEnd，起步急停回退 StopL/R。");
            LoggedMissingStartEnd = true;
        }

        FootSide foot = FootCycle.CaptureForStop();
        StopKey = foot == FootSide.Left ? AnimationKey.StopL : AnimationKey.StopR;
        if (Animation.HasClip(StopKey))
            return true;

        AnimationKey fallback = StopKey == AnimationKey.StopL ? AnimationKey.StopR : AnimationKey.StopL;
        if (Animation.HasClip(fallback))
        {
            StopKey = fallback;
            return true;
        }

        if (!LoggedMissingStop)
        {
            Debug.LogError("LocomotionStateMachine: AnimationProfile 未绑定 StopL/StopR，急停直接回 Idle。");
            LoggedMissingStop = true;
        }

        return false;
    }

    /// <summary>优先用调用方指定朝向，否则用当前根朝向。</summary>
    public Vector3 ResolveStopEnterFacing(Vector3 preferredFacing)
    {
        Vector3 facing = preferredFacing.sqrMagnitude > 0.0001f ? preferredFacing : Root.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f)
            return Vector3.forward;
        return facing.normalized;
    }

    /// <summary>
    /// 准备并切入 Stop；无法解析 Clip 时强制 Idle。
    /// fromStart 决定 StartEnd；hardCut 跳过 InterruptFade。
    /// </summary>
    public void GoStop(bool fromStart, Vector3 preferredFacing = default, bool hardCut = false)
    {
        StopFromStart = fromStart;
        StopPlayHardCut = hardCut;
        StopEnterFacing = ResolveStopEnterFacing(preferredFacing);
        if (!TryResolveStopKey(fromStart))
        {
            RequestPhase(LocomotionPhase.Idle, force: true);
            return;
        }

        RequestPhase(LocomotionPhase.Stop, force: true);
    }

    /// <summary>准备并切入 Gait；可选硬切 Play 与对齐朝向。</summary>
    public void GoGait(
        LocomotionGait gait,
        bool hardCutPlay = false,
        Vector3 faceDirection = default)
    {
        LocomotionGaitPolicy policy = Profile != null ? Profile.GaitPolicy : null;
        PendingGait = policy != null ? policy.ClampGait(gait) : gait;
        PendingGaitHardCutPlay = hardCutPlay;
        PendingGaitFaceDirection = faceDirection;
        RequestPhase(LocomotionPhase.Gait, force: true);
    }

    /// <summary>准备 Pivot 目标并切入；缺 Clip 时忽略。</summary>
    public bool TryGoPivot(Vector3 worldDirection)
    {
        if (!Animation.HasClip(AnimationKey.PivotTurn))
        {
            if (!LoggedMissingPivot)
            {
                Debug.LogError("LocomotionStateMachine: AnimationProfile 未绑定 PivotTurn Clip，已忽略转身。");
                LoggedMissingPivot = true;
            }

            return false;
        }

        PivotTargetDirection = worldDirection.sqrMagnitude > 0.001f
            ? worldDirection.normalized
            : Root.forward;
        RequestPhase(LocomotionPhase.PivotTurn, force: true);
        return true;
    }

    /// <summary>
    /// Stop/Pivot 烘焙位移；TurnBack 解锁后把输入变化作为动画转身附加偏移，
    /// 并把后续局部位移转到修正后的角色根朝向。
    /// </summary>
    public void ApplyBakedRootMotion(
        LocomotionPhase phase,
        in LocomotionMotorCommand command,
        float deltaTime)
    {
        bool inputOwnsPivotRotation = phase == LocomotionPhase.PivotTurn
            && command.RotationMode == LocomotionRotationMode.PivotTarget;
        bool applyYaw = phase == LocomotionPhase.PivotTurn
            && Profile != null
            && Profile.PivotApplyRootYaw
            && !inputOwnsPivotRotation;

        if (inputOwnsPivotRotation)
            Motor.ApplyLocomotion(command, deltaTime);
        else if (phase == LocomotionPhase.PivotTurn && !applyYaw)
            Motor.FaceWorldDirection(PivotEnterFacing);
        else if (phase == LocomotionPhase.Stop)
            Motor.FaceWorldDirection(StopEnterFacing);

        // 位移权威为逻辑帧索引，禁止再读 Animation.NormalizedTime
        if (!RootMotionPlayer.TryConsume(
                applyYaw,
                out Vector3 worldDelta,
                out float yawDelta))
            return;

        if (inputOwnsPivotRotation)
            worldDelta = ReorientPivotDeltaToCurrentFacing(worldDelta);

        Motor.MovePlanar(worldDelta, deltaTime);
        if (applyYaw)
            Motor.ApplyYawDegrees(yawDelta);
    }

    /// <summary>把以 Pivot 进入朝向烘焙的世界位移转到当前角色根朝向。</summary>
    public Vector3 ReorientPivotDeltaToCurrentFacing(Vector3 worldDelta)
    {
        Vector3 currentFacing = Root.forward;
        currentFacing.y = 0f;
        if (currentFacing.sqrMagnitude < 0.0001f || PivotEnterFacing.sqrMagnitude < 0.0001f)
            return worldDelta;

        Quaternion enterRotation = Quaternion.LookRotation(PivotEnterFacing.normalized);
        Quaternion currentRotation = Quaternion.LookRotation(currentFacing.normalized);
        return currentRotation * Quaternion.Inverse(enterRotation) * worldDelta;
    }

    /// <summary>
    /// Clip 已负责从进入朝向转到初始折返方向；代码只叠加玩家后来修改的输入偏移。
    /// </summary>
    public Vector3 ResolvePivotSteeringRootDirection()
    {
        Vector3 initialTarget = PivotInitialTargetDirection;
        Vector3 currentTarget = PivotTargetDirection;
        initialTarget.y = 0f;
        currentTarget.y = 0f;
        if (initialTarget.sqrMagnitude < 0.0001f || currentTarget.sqrMagnitude < 0.0001f)
            return PivotEnterFacing;

        Quaternion initialRotation = Quaternion.LookRotation(initialTarget.normalized);
        Quaternion currentRotation = Quaternion.LookRotation(currentTarget.normalized);
        Quaternion inputOffset = currentRotation * Quaternion.Inverse(initialRotation);
        return inputOffset * PivotEnterFacing;
    }

    /// <summary>当前相位 Clip 是否播完。</summary>
    public bool IsCurrentPhaseClipFinished() => Animation.HasFinishedCurrent;

    /// <summary>Start→Gait 归一化门槛或 Clip 结束。</summary>
    public bool IsStartFinished()
    {
        float gate = Profile != null ? Profile.StartToGaitNormalized : 1f;
        if (gate < 0.999f && Animation.NormalizedTime >= gate)
            return true;
        return IsCurrentPhaseClipFinished();
    }
}

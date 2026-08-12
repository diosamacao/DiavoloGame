using UnityEngine;

/// <summary>
/// 内层 Locomotion 状态机共享上下文：依赖、跨相位数据与帧快照；
/// 相位决策留在各 State，此处只提供执行辅助。
/// </summary>
public sealed class LocomotionContext
{
    LocomotionStateMachine _machine;
    ILocomotionFacingTargetSource _facingTargetSource;

    /// <summary>创建上下文并注入运行时依赖。</summary>
    public LocomotionContext(
        Transform root,
        CharacterMotor motor,
        CharacterAnimationService animation,
        IMoveIntentSource moveIntent,
        CharacterLocomotionProfile profile,
        LocomotionFootCycle footCycle,
        LocomotionFootstepPlayer footstepPlayer,
        LocomotionRootMotionPlayer rootMotionPlayer,
        ILocomotionAnimResolver animResolver = null)
    {
        Root = root;
        Motor = motor;
        Animation = animation;
        Input = moveIntent;
        Profile = profile;
        FootCycle = footCycle;
        FootstepPlayer = footstepPlayer;
        RootMotionPlayer = rootMotionPlayer;
        AnimResolver = animResolver
            ?? new DefaultLocomotionAnimResolver(
                profile != null ? profile.AnimSet : null,
                profile != null ? profile.CardinalEpsilon : LocomotionDirectionModel.DefaultEpsilon);
        SprintLean = new SprintLeanModel();
    }

    public Transform Root { get; }
    public CharacterMotor Motor { get; }
    public CharacterAnimationService Animation { get; }
    /// <summary>角色无关移动意图源（玩家 InputManager / AI Desire）。</summary>
    public IMoveIntentSource Input { get; }
    public CharacterLocomotionProfile Profile { get; }
    public LocomotionFootCycle FootCycle { get; }
    public LocomotionFootstepPlayer FootstepPlayer { get; }
    public LocomotionRootMotionPlayer RootMotionPlayer { get; }

    /// <summary>步态+局部输入 → AnimationKey。</summary>
    public ILocomotionAnimResolver AnimResolver { get; }

    /// <summary>L-DIR4 Sprint 倾身状态；Visual 只读 Roll。</summary>
    public SprintLeanModel SprintLean { get; }

    /// <summary>当前视觉倾身 Roll（度）；权威根不受影响。</summary>
    public float SprintLeanRollDegrees =>
        SprintLeanModel.ToRollDegrees(
            SprintLean.Lean01,
            Profile != null ? Profile.SprintLean : null);

    /// <summary>当前稳态步态（Gait 相位内可变）。</summary>
    public LocomotionGait Gait { get; set; } = LocomotionGait.Walk;

    /// <summary>切入 Gait 前写入的目标步态。</summary>
    public LocomotionGait PendingGait { get; set; } = LocomotionGait.Walk;

    /// <summary>Gait.Enter 时硬切并立即 Play 目标步态 Clip（Resume / Pivot 结束）。</summary>
    public bool PendingGaitHardCutPlay { get; set; }

    /// <summary>Gait.Enter 后可选对齐的朝向；零向量表示跳过。</summary>
    public Vector3 PendingGaitFaceDirection { get; set; }

    /// <summary>Pivot 期间持续刷新的输入目标朝向（结束接 Sprint/Stop 用）。</summary>
    public Vector3 PivotTargetDirection { get; set; } = Vector3.forward;

    /// <summary>进入 Pivot 瞬间的根朝向；烘焙局部位移→世界的旋转基。</summary>
    public Vector3 PivotEnterFacing { get; set; } = Vector3.forward;

    /// <summary>本次 Pivot 是否出现过移动输入；结束时用于直入 Sprint。</summary>
    public bool PivotMoveLatched { get; set; }

    /// <summary>进入 Stop 时的朝向；烘焙根位移局部→世界基。</summary>
    public Vector3 StopEnterFacing { get; set; } = Vector3.forward;

    /// <summary>当前急停使用的 AnimationKey（StartEnd / StopL / StopR）。</summary>
    public AnimationKey StopKey { get; set; } = AnimationKey.StopR;

    /// <summary>本次 Start 相位锁定的起步 Key（由 AnimSet.ResolveStart 写入）。</summary>
    public AnimationKey ActiveStartKey { get; set; } = AnimationKey.Start;

    /// <summary>闩定起步时的步态档（Walk/Run）；升档/降档判断用，禁止 State 认 Key 族。</summary>
    public LocomotionGait ActiveStartGait { get; set; } = LocomotionGait.Walk;

    /// <summary>Start 进入时闩定的 cardinal；微抖不换片。</summary>
    public MoveCardinal ActiveStartCardinal { get; set; } = MoveCardinal.Forward;

    /// <summary>Gait 循环当前滞回 cardinal。</summary>
    public MoveCardinal GaitCardinal { get; set; } = MoveCardinal.None;

    /// <summary>当前 GaitCardinal 已连续驻留的逻辑帧数。</summary>
    public int GaitCardinalDwellFrames { get; set; }

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
        // 与玩法同帧采样 wish，供脚底调试箭头在渲染帧稳定显示
        Motor.CaptureDebugWishWorldDirection(hasMove ? worldDir : Vector3.zero);
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

    /// <summary>按初始步态 + DirectionModel 闩定起步槽；写入 Key/Gait/Cardinal。</summary>
    public AnimationKey ResolveAndLatchStartKey(float magnitude, Vector2 localMoveIntent)
    {
        LocomotionGait gait = ResolveInitialGait(magnitude);
        float eps = Profile != null ? Profile.CardinalEpsilon : LocomotionDirectionModel.DefaultEpsilon;
        // Start 闩定时 FrameSnapshot 可能尚未刷新：用入参意图；FaceTarget 下仍以摇杆本地闩（进 Gait 后转世界本地）
        MoveCardinal cardinal = LocomotionDirectionModel.Resolve(localMoveIntent, eps);
        if (cardinal == MoveCardinal.None)
            cardinal = MoveCardinal.Forward;

        ActiveStartGait = gait;
        ActiveStartCardinal = cardinal;
        LocomotionAnimSet set = Profile != null ? Profile.AnimSet : LocomotionAnimSet.CreateDefault();
        ActiveStartKey = set.ResolveStart(gait, cardinal, Animation);
        return ActiveStartKey;
    }

    /// <summary>重置 Gait cardinal 滞回（进 Gait / 硬切步态时）。</summary>
    public void ResetGaitCardinal()
    {
        GaitCardinal = MoveCardinal.None;
        GaitCardinalDwellFrames = 0;
    }

    /// <summary>提案 cardinal 经最短驻留后采纳；委托 <see cref="LocomotionCardinalHysteresis"/>。</summary>
    public MoveCardinal ResolveGaitCardinalWithHysteresis(MoveCardinal proposed, int minDwellFrames)
    {
        MoveCardinal current = GaitCardinal;
        int dwell = GaitCardinalDwellFrames;
        MoveCardinal resolved = LocomotionCardinalHysteresis.Resolve(
            ref current,
            ref dwell,
            proposed,
            minDwellFrames);
        GaitCardinal = current;
        GaitCardinalDwellFrames = dwell;
        return resolved;
    }

    /// <summary>绑定 FaceTarget 朝向源（工厂在 TargetLock 创建后调用）。</summary>
    public void BindFacingTargetSource(ILocomotionFacingTargetSource source) =>
        _facingTargetSource = source;

    /// <summary>
    /// 运行时有效朝向：FaceCamera 保持配置；有目标时 FollowMove/FaceTarget→FaceTarget；无目标 FaceTarget→FollowMove。
    /// </summary>
    public LocomotionFacingMode ResolveFacingMode()
    {
        LocomotionFacingMode configured = Profile != null
            ? Profile.FacingMode
            : LocomotionFacingMode.FollowMove;

        if (configured == LocomotionFacingMode.FaceCamera)
            return LocomotionFacingMode.FaceCamera;

        bool hasTarget = _facingTargetSource != null
            && _facingTargetSource.TryGetFacingWorldDirection(out _);

        if (configured == LocomotionFacingMode.FaceTarget)
            return hasTarget ? LocomotionFacingMode.FaceTarget : LocomotionFacingMode.FollowMove;

        // FollowMove：索敌锁或软锁命中时升格 FaceTarget，解锁自动回退
        return hasTarget ? LocomotionFacingMode.FaceTarget : LocomotionFacingMode.FollowMove;
    }

    /// <summary>Start/Gait/Pivot InputAuth 的 Motor 旋转模式（由有效 FacingMode 映射）。</summary>
    public LocomotionRotationMode ResolveGaitRotationMode() =>
        CharacterLocomotionProfile.ToMotorRotationMode(ResolveFacingMode());

    /// <summary>每逻辑帧把 FaceTarget 方向推给 Motor；非 FaceTarget 时清除。</summary>
    public void PublishFacingTargetToMotor()
    {
        if (ResolveFacingMode() != LocomotionFacingMode.FaceTarget
            || _facingTargetSource == null
            || !_facingTargetSource.TryGetFacingWorldDirection(out Vector3 dir))
        {
            Motor.ClearFaceTargetWorldDirection();
            return;
        }

        Motor.SetFaceTargetWorldDirection(dir);
    }

    /// <summary>
    /// 选片用本地意图：FaceTarget/FaceCamera 用世界 wish→角色本地；FollowMove 用摇杆本地。
    /// </summary>
    public Vector2 ResolveCardinalLocalMove()
    {
        LocomotionFacingMode mode = ResolveFacingMode();
        if (mode == LocomotionFacingMode.FollowMove)
            return FrameSnapshot.MoveIntent;

        Vector3 worldWish = FrameSnapshot.WorldMoveDirection;
        if (worldWish.sqrMagnitude < 0.0001f)
            return Vector2.zero;

        Vector3 facing = Root.forward;
        // FaceTarget：优先用锁定方向作本地前向基，避免朝向尚未跟上时 cardinal 抖
        if (mode == LocomotionFacingMode.FaceTarget
            && _facingTargetSource != null
            && _facingTargetSource.TryGetFacingWorldDirection(out Vector3 lockFwd))
        {
            facing = lockFwd;
        }

        return LocomotionDirectionModel.ToLocalMoveIntent(worldWish, facing);
    }

    /// <summary>Run / Sprint 视为跑档急停门槛。</summary>
    public static bool IsRunTier(LocomotionGait gait) =>
        gait == LocomotionGait.Run || gait == LocomotionGait.Sprint;

    /// <summary>经 AnimSet + Cardinal 滞回解析本帧循环键。</summary>
    public AnimationKey ResolveLocomotionAnimationKey()
    {
        LocomotionGait gait = Gait;
        float eps = Profile != null ? Profile.CardinalEpsilon : LocomotionDirectionModel.DefaultEpsilon;
        int dwell = Profile != null ? Profile.CardinalMinDwellFrames : 3;
        MoveCardinal proposed = LocomotionDirectionModel.Resolve(ResolveCardinalLocalMove(), eps);
        MoveCardinal cardinal = ResolveGaitCardinalWithHysteresis(proposed, dwell);

        LocomotionAnimSet set = Profile != null ? Profile.AnimSet : LocomotionAnimSet.CreateDefault();
        AnimationKey key = set.ResolveLoop(gait, cardinal, Animation);

        // Sprint 缺 Clip：首次告警后 AnimSet 已回退 Run
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

    /// <summary>Profile AnimSet 是否有任一起步 Clip。</summary>
    public bool HasAnyStartClip()
    {
        LocomotionAnimSet set = Profile != null ? Profile.AnimSet : LocomotionAnimSet.CreateDefault();
        return set.HasAnyStartClip(Animation);
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
    /// Stop/Pivot AnimAuth 烘焙位移。
    /// Pivot：强制吃烘焙偏航；Stop：锁进入朝向且不吃偏航。
    /// 位移权威为逻辑帧索引，禁止再读 Animation.NormalizedTime。
    /// </summary>
    public void ApplyBakedRootMotion(LocomotionPhase phase, float deltaTime)
    {
        bool applyYaw = phase == LocomotionPhase.PivotTurn;
        if (phase == LocomotionPhase.Stop)
            Motor.FaceWorldDirection(StopEnterFacing);

        if (!RootMotionPlayer.TryConsume(
                applyYaw,
                out Vector3 worldDelta,
                out float yawDelta))
            return;

        Motor.MovePlanar(worldDelta, deltaTime);
        if (applyYaw)
            Motor.ApplyYawDegrees(yawDelta);
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

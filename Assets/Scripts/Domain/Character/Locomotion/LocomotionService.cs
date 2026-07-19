using UnityEngine;

/// <summary>Locomotion 相位决策：Start / Gait(Walk|Run|Sprint) / PivotTurn / Stop，并驱动 Motor 与动画。</summary>
public sealed class LocomotionService
{
    readonly Transform _root;
    readonly CharacterMotor _motor;
    readonly CharacterAnimationService _animation;
    readonly InputManager _input;
    readonly CharacterLocomotionProfile _profile;
    readonly LocomotionFootCycle _footCycle = new();
    readonly LocomotionFootstepPlayer _footstepPlayer;
    readonly LocomotionRootMotionPlayer _rootMotionPlayer;

    LocomotionPhase _phase = LocomotionPhase.Idle;
    LocomotionGait _gait = LocomotionGait.Walk;
    Vector3 _pivotTargetDirection = Vector3.forward;
    /// <summary>进入 Pivot 瞬间的根朝向；含 Y 转向的 Clip 播放期间锁在此方向，避免与代码转根双重叠加。</summary>
    Vector3 _pivotEnterFacing = Vector3.forward;
    /// <summary>进入 Stop 时的朝向；烘焙根位移的局部→世界基。</summary>
    Vector3 _stopEnterFacing = Vector3.forward;
    AnimationKey _stopKey = AnimationKey.StopR;
    /// <summary>当前在 Run 步态下已连续保持跑输入的时长；进 Sprint 或离开跑档时清零。</summary>
    float _runHoldSeconds;
    /// <summary>本次 Pivot 期间是否出现过移动输入；用于结束时衔接 Sprint，避免单帧松手误进 Stop→Start。</summary>
    bool _pivotMoveLatched;
    /// <summary>Gait 下连续无移动输入的累计时间；未超过宽限则不进 Stop。</summary>
    float _gaitInputGapSeconds;
    bool _loggedMissingStart;
    bool _loggedMissingPivot;
    bool _loggedMissingStop;
    bool _loggedMissingStartEnd;
    bool _loggedMissingSprint;

    /// <summary>当前内嵌相位（调试 / 外部只读）。</summary>
    public LocomotionPhase Phase => _phase;

    /// <summary>当前稳态步态。</summary>
    public LocomotionGait Gait => _gait;

    public LocomotionService(
        Transform root,
        CharacterMotor motor,
        CharacterAnimationService animation,
        InputManager input,
        CharacterLocomotionProfile profile,
        LocomotionFootstepPlayer footstepPlayer)
    {
        _root = root;
        _motor = motor;
        _animation = animation;
        _input = input;
        _profile = profile;
        _footstepPlayer = footstepPlayer;
        _rootMotionPlayer = new LocomotionRootMotionPlayer(profile);
    }

    /// <summary>进入顶层 Locomotion；可消费 Action 边界传入的一次性步态恢复请求。</summary>
    public void Enter(in LocomotionResumeRequest resumeRequest)
    {
        _phase = LocomotionPhase.Idle;
        _runHoldSeconds = 0f;
        _gaitInputGapSeconds = 0f;
        _rootMotionPlayer.End();
        _footCycle.Unfreeze();
        _footCycle.SetMarkers(System.Array.Empty<FootPlantMarker>());

        bool canResume = resumeRequest.IsValid
            && (!resumeRequest.RequireMoveIntent || _input.HasMoveIntent);
        if (canResume && resumeRequest.SkipStart)
        {
            // Dodge 恢复属于显式过渡语义：直接进目标步态，不走 Idle→Start→Run 计时。
            EnterGait(resumeRequest.InitialGait);
            _animation.ResetPlaybackState();
            _animation.Play(ResolveGaitAnimationKey(resumeRequest.InitialGait), 0f);
        }
    }

    /// <summary>离开顶层 Locomotion（进 Action 等）；停止落脚采样与派发。</summary>
    public void Exit()
    {
        _footCycle.Freeze();
        _rootMotionPlayer.End();
        _phase = LocomotionPhase.Idle;
        _runHoldSeconds = 0f;
        _gaitInputGapSeconds = 0f;
    }

    /// <summary>推进相位、位移命令、动画与脚步。</summary>
    public void Tick(float deltaTime)
    {
        LocomotionInputSnapshot snapshot = BuildSnapshot();
        EvaluateTransitions(snapshot, deltaTime);
        TickFootCycle();
        // 先保证当前相位 Clip 在播，再按 NormalizedTime 消费烘焙根位移
        PlayPhaseAnimation();
        LocomotionMotorCommand command = BuildMotorCommand(snapshot);
        if (UsesBakedRootMotion)
            ApplyBakedRootMotion(deltaTime);
        else
            _motor.ApplyLocomotion(command, deltaTime);
        _footstepPlayer.PlayIfPlanted(_footCycle.PlantedThisFrame);
    }

    bool UsesBakedRootMotion =>
        _rootMotionPlayer.IsActive
        && (_phase == LocomotionPhase.Stop || _phase == LocomotionPhase.PivotTurn);

    /// <summary>Stop/Pivot：用烘焙轨位移；Pivot 可选烘焙偏航，否则锁进入朝向。</summary>
    void ApplyBakedRootMotion(float deltaTime)
    {
        bool applyYaw = _phase == LocomotionPhase.PivotTurn
            && _profile != null
            && _profile.PivotApplyRootYaw;

        if (_phase == LocomotionPhase.PivotTurn && !applyYaw)
            _motor.FaceWorldDirection(_pivotEnterFacing);
        else if (_phase == LocomotionPhase.Stop)
            _motor.FaceWorldDirection(_stopEnterFacing);

        if (_rootMotionPlayer.TryConsume(
                _animation.NormalizedTime,
                applyYaw,
                out Vector3 worldDelta,
                out float yawDelta))
        {
            _motor.MovePlanar(worldDelta, deltaTime);
            if (applyYaw)
                _motor.ApplyYawDegrees(yawDelta);
        }
    }

    LocomotionInputSnapshot BuildSnapshot()
    {
        Vector2 moveIntent = _input.MoveIntent;
        float magnitude = _input.MoveMagnitude;
        bool hasMove = _input.HasMoveIntent;
        Vector3 worldDir = _motor.ResolveWorldMoveDirection(moveIntent);
        return new LocomotionInputSnapshot(
            moveIntent,
            magnitude,
            hasMove,
            worldDir,
            _motor.IsGrounded,
            _motor.PlanarSpeedEstimate);
    }

    void EvaluateTransitions(in LocomotionInputSnapshot snapshot, float deltaTime)
    {
        float idleThreshold = _profile != null ? _profile.IdleInputThreshold : 0.01f;
        bool hasMove = snapshot.HasMoveInput && snapshot.Magnitude >= idleThreshold;

        if (hasMove)
            _gaitInputGapSeconds = 0f;

        // Pivot 中持续刷新目标朝向，并锁存「仍要冲刺」意图
        if (_phase == LocomotionPhase.PivotTurn && hasMove && snapshot.WorldMoveDirection.sqrMagnitude > 0.001f)
        {
            _pivotTargetDirection = snapshot.WorldMoveDirection.normalized;
            _pivotMoveLatched = true;
        }

        // 1) Start / Pivot 松输入 → 立刻 Stop（Start 用 StartEnd Clip；Pivot 结束帧用锁存，见步骤 7）
        if (!hasMove && _phase == LocomotionPhase.Start)
        {
            EnterStop();
            return;
        }

        // Pivot 中松手：急停朝向用转身目标，避免根仍锁在转身前朝向导致急停「扭回去」
        if (!hasMove && _phase == LocomotionPhase.PivotTurn && !IsCurrentPhaseClipFinished())
        {
            EnterStop(_pivotTargetDirection);
            return;
        }

        // 2) Gait 松输入：短时宽限内保持步态（便于 A→D 换向进 Pivot），超时再 Stop/Idle
        if (!hasMove && _phase == LocomotionPhase.Gait)
        {
            _gaitInputGapSeconds += deltaTime;
            float grace = _profile != null ? _profile.GaitInputGapGraceSeconds : 0.15f;
            if (_gaitInputGapSeconds < grace)
                return;

            _gaitInputGapSeconds = 0f;
            float stopMin = _motor.RunSpeed * (_profile != null ? _profile.StopMinSpeedFactor : 0.5f);
            if (snapshot.PlanarSpeed >= stopMin || IsRunTier(_gait))
                EnterStop();
            else
                EnterIdle();
            return;
        }

        // 3) Stop：任意时刻有移动输入均可取消进 Start（含后半段）；播完无输入 → Idle
        if (_phase == LocomotionPhase.Stop)
        {
            if (hasMove)
            {
                EnterStart();
                return;
            }

            if (IsCurrentPhaseClipFinished())
                EnterIdle();

            return;
        }

        // 4) Idle → Start（必经）
        if (_phase == LocomotionPhase.Idle)
        {
            if (hasMove)
                EnterStart();
            return;
        }

        // 5) 仅 Sprint 大角度 → Pivot
        if (_phase == LocomotionPhase.Gait && hasMove && CanEnterPivot(snapshot))
        {
            EnterPivotTurn(snapshot.WorldMoveDirection);
            return;
        }

        // 6) Start 播完 → Gait（Walk 或 Run，不会直接 Sprint）
        if (_phase == LocomotionPhase.Start && IsStartFinished())
        {
            EnterGait(ResolveInitialGait(snapshot.Magnitude));
            return;
        }

        // 7) Pivot 播完：对齐朝向后直接回 Sprint（勿经 Stop/Start）
        if (_phase == LocomotionPhase.PivotTurn && IsCurrentPhaseClipFinished())
        {
            FinishPivotTurn(snapshot, hasMove);
            return;
        }

        // 8) Gait 内：Walk/Run/Sprint 升级与降档
        if (_phase == LocomotionPhase.Gait && hasMove)
            UpdateGaitWhileMoving(snapshot.Magnitude, deltaTime);
    }

    /// <summary>跑输入持续累计；满时长 Run→Sprint；降到走输入则回 Walk 并清计时。</summary>
    void UpdateGaitWhileMoving(float magnitude, float deltaTime)
    {
        bool wantRunTier = magnitude > _motor.RunThreshold;

        if (!wantRunTier)
        {
            _runHoldSeconds = 0f;
            if (_gait != LocomotionGait.Walk)
                SetGait(LocomotionGait.Walk);
            return;
        }

        // 满跑输入：Walk→Run；Run 计时→Sprint；已在 Sprint 则保持
        if (_gait == LocomotionGait.Walk)
        {
            _runHoldSeconds = 0f;
            SetGait(LocomotionGait.Run);
            return;
        }

        if (_gait == LocomotionGait.Run)
        {
            _runHoldSeconds += deltaTime;
            float need = _profile != null ? _profile.SprintAfterRunSeconds : 3f;
            if (_runHoldSeconds >= need)
            {
                _runHoldSeconds = 0f;
                SetGait(LocomotionGait.Sprint);
            }

            return;
        }

        // Sprint：保持冲刺，直到输入降到走档（上面已处理）
    }

    bool CanEnterPivot(in LocomotionInputSnapshot snapshot)
    {
        if (_gait != LocomotionGait.Sprint)
            return false;
        if (snapshot.WorldMoveDirection.sqrMagnitude < 0.001f)
            return false;

        // 对齐 zzzdemo：角色 forward 与输入目标方向夹角（turnBackAngle 默认 135）
        float pivotAngle = _profile != null ? _profile.PivotAngleDegrees : 135f;
        Vector3 facing = _root.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f)
            return false;

        float angleCurrent = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(snapshot.WorldMoveDirection.x, snapshot.WorldMoveDirection.z) * Mathf.Rad2Deg;
        float yawError = Mathf.Abs(Mathf.DeltaAngle(angleCurrent, targetAngle));
        return yawError > pivotAngle;
    }

    /// <summary>转身结束：先硬切朝前的 Sprint/Stop，再转根对齐输入，避免「转身末帧本地 180° + 新根朝向」叠成旧方向闪一下。</summary>
    void FinishPivotTurn(in LocomotionInputSnapshot snapshot, bool hasMove)
    {
        Vector3 faceDir = snapshot.WorldMoveDirection.sqrMagnitude > 0.001f
            ? snapshot.WorldMoveDirection
            : _pivotTargetDirection;

        bool resumeSprint = _pivotMoveLatched || hasMove;
        _pivotMoveLatched = false;

        _rootMotionPlayer.End();

        if (resumeSprint)
        {
            EnterGait(LocomotionGait.Sprint);
            _animation.ResetPlaybackState();
            // 必须先去掉转身末帧姿态，再 Face；顺序反了会闪回进入时朝向
            _animation.Play(ResolveGaitAnimationKey(LocomotionGait.Sprint), 0f);
            _motor.FaceWorldDirection(faceDir);
            _motor.ResetRotationDamping();
            return;
        }

        EnterStop(faceDir);
        if (_phase != LocomotionPhase.Stop)
            return;

        // EnterStop 已 ResetPlayback；硬切急停并钉在目标朝向
        _animation.Play(_stopKey, 0f);
        _motor.FaceWorldDirection(faceDir);
        _motor.ResetRotationDamping();
    }

    /// <summary>起步结束时的初始步态：只进 Walk 或 Run，不直接 Sprint。</summary>
    LocomotionGait ResolveInitialGait(float magnitude) =>
        magnitude > _motor.RunThreshold ? LocomotionGait.Run : LocomotionGait.Walk;

    static bool IsRunTier(LocomotionGait gait) =>
        gait == LocomotionGait.Run || gait == LocomotionGait.Sprint;

    void EnterIdle()
    {
        _phase = LocomotionPhase.Idle;
        _gait = LocomotionGait.Walk;
        _runHoldSeconds = 0f;
        _gaitInputGapSeconds = 0f;
        _rootMotionPlayer.End();
        _footCycle.Freeze();
        _footCycle.SetMarkers(System.Array.Empty<FootPlantMarker>());
    }

    void EnterStart()
    {
        _runHoldSeconds = 0f;
        _gaitInputGapSeconds = 0f;
        _rootMotionPlayer.End();
        if (!_animation.HasClip(AnimationKey.Start))
        {
            if (!_loggedMissingStart)
            {
                Debug.LogError("LocomotionService: AnimationProfile 未绑定 Start Clip，已跳过起步直接进 Gait。");
                _loggedMissingStart = true;
            }

            EnterGait(ResolveInitialGait(_input.MoveMagnitude));
            return;
        }

        _phase = LocomotionPhase.Start;
        _footCycle.Unfreeze();
        _footCycle.SetMarkers(_profile != null ? _profile.StartFootPlants : System.Array.Empty<FootPlantMarker>());
        _animation.ResetPlaybackState();
    }

    void EnterGait(LocomotionGait gait)
    {
        _phase = LocomotionPhase.Gait;
        _rootMotionPlayer.End();
        if (gait == LocomotionGait.Run)
            _runHoldSeconds = 0f;
        else if (gait != LocomotionGait.Sprint)
            _runHoldSeconds = 0f;

        SetGait(gait);
        _footCycle.Unfreeze();
    }

    void SetGait(LocomotionGait gait)
    {
        _gait = gait;
        _footCycle.SetMarkers(GetMarkersForCurrentPhase());
    }

    void EnterPivotTurn(Vector3 worldDirection)
    {
        if (!_animation.HasClip(AnimationKey.PivotTurn))
        {
            if (!_loggedMissingPivot)
            {
                Debug.LogError("LocomotionService: AnimationProfile 未绑定 PivotTurn Clip，已忽略转身。");
                _loggedMissingPivot = true;
            }

            return;
        }

        _phase = LocomotionPhase.PivotTurn;
        _pivotMoveLatched = true;
        _pivotTargetDirection = worldDirection.sqrMagnitude > 0.001f
            ? worldDirection.normalized
            : _root.forward;
        // 锁进入时朝向：Clip 若自带 180° 转向，代码再 FollowInput 会在中段叠成「朝回旧方向」
        _pivotEnterFacing = _root.forward;
        _pivotEnterFacing.y = 0f;
        if (_pivotEnterFacing.sqrMagnitude < 0.0001f)
            _pivotEnterFacing = Vector3.forward;
        else
            _pivotEnterFacing.Normalize();

        _motor.ResetRotationDamping();
        _motor.FaceWorldDirection(_pivotEnterFacing);
        _footCycle.Freeze();
        _footCycle.SetMarkers(System.Array.Empty<FootPlantMarker>());
        _animation.ResetPlaybackState();
        // 转身起手尽量硬切，避免与 Sprint CrossFade 把朝向混花
        _animation.Play(AnimationKey.PivotTurn, 0f);
        _rootMotionPlayer.Begin(AnimationKey.PivotTurn, Quaternion.LookRotation(_pivotEnterFacing));
    }

    /// <summary>
    /// 进入急停。preferredFacing：从 Pivot 切入时传入转身目标朝向，
    /// 避免根节点仍停在转身前朝向导致急停瞬间扭回旧方向。
    /// 从 Start 切入时优先播 StartEnd（Run_Start_End）；缺绑则回退 StopL/R。
    /// </summary>
    void EnterStop(Vector3 preferredFacing = default)
    {
        // 须在改 phase 前判定来源：起步秒停用独立收束 Clip
        bool fromStart = _phase == LocomotionPhase.Start;
        _runHoldSeconds = 0f;
        _gaitInputGapSeconds = 0f;
        _pivotMoveLatched = false;

        if (!TryResolveStopKey(fromStart))
            return;

        _phase = LocomotionPhase.Stop;
        _stopEnterFacing = ResolveStopEnterFacing(preferredFacing);
        _motor.FaceWorldDirection(_stopEnterFacing);

        _footCycle.SetMarkers(System.Array.Empty<FootPlantMarker>());
        _animation.ResetPlaybackState();
        _animation.Play(_stopKey, _profile != null ? _profile.InterruptFadeDuration : 0.08f);
        _rootMotionPlayer.Begin(_stopKey, Quaternion.LookRotation(_stopEnterFacing));
    }

    /// <summary>
    /// 解析急停 Clip：Start→StartEnd；否则按落脚 StopL/R。
    /// 失败时进 Idle 并返回 false。
    /// </summary>
    bool TryResolveStopKey(bool fromStart)
    {
        if (fromStart && _animation.HasClip(AnimationKey.StartEnd))
        {
            _stopKey = AnimationKey.StartEnd;
            return true;
        }

        if (fromStart && !_loggedMissingStartEnd)
        {
            Debug.LogError("LocomotionService: AnimationProfile 未绑定 StartEnd，起步急停回退 StopL/R。");
            _loggedMissingStartEnd = true;
        }

        FootSide foot = _footCycle.CaptureForStop();
        _stopKey = foot == FootSide.Left ? AnimationKey.StopL : AnimationKey.StopR;
        if (_animation.HasClip(_stopKey))
            return true;

        AnimationKey fallback = _stopKey == AnimationKey.StopL ? AnimationKey.StopR : AnimationKey.StopL;
        if (_animation.HasClip(fallback))
        {
            _stopKey = fallback;
            return true;
        }

        if (!_loggedMissingStop)
        {
            Debug.LogError("LocomotionService: AnimationProfile 未绑定 StopL/StopR，急停直接回 Idle。");
            _loggedMissingStop = true;
        }

        EnterIdle();
        return false;
    }

    /// <summary>优先用调用方指定朝向，否则用当前根朝向。</summary>
    Vector3 ResolveStopEnterFacing(Vector3 preferredFacing)
    {
        Vector3 facing = preferredFacing.sqrMagnitude > 0.0001f ? preferredFacing : _root.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f)
            return Vector3.forward;
        return facing.normalized;
    }

    FootPlantMarker[] GetMarkersForCurrentPhase()
    {
        if (_profile == null)
            return System.Array.Empty<FootPlantMarker>();
        if (_phase == LocomotionPhase.Start)
            return _profile.StartFootPlants;
        if (_phase == LocomotionPhase.Gait)
            return _profile.GetGaitFootPlants(_gait);
        return System.Array.Empty<FootPlantMarker>();
    }

    void TickFootCycle()
    {
        if (_phase != LocomotionPhase.Gait && _phase != LocomotionPhase.Start)
        {
            _footCycle.Freeze();
            return;
        }

        _footCycle.Unfreeze();
        _footCycle.Tick(_animation.NormalizedTime);
    }

    /// <summary>
    /// Pivot 位移由烘焙根运动负责时关闭输入推移。
    /// 无烘焙时：可选 pivotRootFollowsInput（ReturnRun）或锁进入朝向。
    /// </summary>
    LocomotionMotorCommand BuildPivotMotorCommand(in LocomotionInputSnapshot snapshot)
    {
        if (_rootMotionPlayer.IsActive)
        {
            return new LocomotionMotorCommand(
                false,
                LocomotionRotationMode.Hold,
                _pivotEnterFacing,
                LocomotionGait.Sprint);
        }

        bool rootFollows = _profile != null && _profile.PivotRootFollowsInput;
        if (!rootFollows)
        {
            _motor.FaceWorldDirection(_pivotEnterFacing);
            return new LocomotionMotorCommand(
                false,
                LocomotionRotationMode.Hold,
                _pivotEnterFacing,
                LocomotionGait.Sprint);
        }

        float lockNorm = _profile.PivotLockNormalizedTime;
        float pivotSmooth = _profile.PivotRotationSmoothTime;
        if (_animation.NormalizedTime < lockNorm)
        {
            _motor.FaceWorldDirection(_pivotEnterFacing);
            return new LocomotionMotorCommand(
                false,
                LocomotionRotationMode.Hold,
                _pivotEnterFacing,
                LocomotionGait.Sprint);
        }

        return new LocomotionMotorCommand(
            false,
            LocomotionRotationMode.FollowInput,
            _pivotTargetDirection,
            LocomotionGait.Sprint,
            pivotSmooth);
    }

    LocomotionMotorCommand BuildMotorCommand(in LocomotionInputSnapshot snapshot)
    {
        switch (_phase)
        {
            case LocomotionPhase.Start:
                return new LocomotionMotorCommand(
                    true,
                    LocomotionRotationMode.FollowInput,
                    Vector3.zero,
                    ResolveInitialGait(snapshot.Magnitude));
            case LocomotionPhase.Gait:
                return new LocomotionMotorCommand(
                    true,
                    LocomotionRotationMode.FollowInput,
                    Vector3.zero,
                    _gait);
            case LocomotionPhase.PivotTurn:
                return BuildPivotMotorCommand(snapshot);
            case LocomotionPhase.Stop:
                return new LocomotionMotorCommand(
                    false,
                    LocomotionRotationMode.Hold,
                    _stopEnterFacing,
                    LocomotionGait.Walk);
            default:
                return new LocomotionMotorCommand(
                    false,
                    LocomotionRotationMode.Hold,
                    Vector3.zero,
                    LocomotionGait.Walk);
        }
    }

    void PlayPhaseAnimation()
    {
        AnimationKey key = ResolveAnimationKey();
        float? fade = null;
        if (_phase == LocomotionPhase.Stop)
        {
            float interrupt = _profile != null ? _profile.InterruptFadeDuration : 0.08f;
            fade = interrupt;
        }

        _animation.Play(key, fade);
    }

    AnimationKey ResolveAnimationKey()
    {
        switch (_phase)
        {
            case LocomotionPhase.Start:
                return AnimationKey.Start;
            case LocomotionPhase.PivotTurn:
                return AnimationKey.PivotTurn;
            case LocomotionPhase.Stop:
                return _stopKey;
            case LocomotionPhase.Gait:
                return ResolveGaitAnimationKey(_gait);
            default:
                return AnimationKey.Idle;
        }
    }

    AnimationKey ResolveGaitAnimationKey(LocomotionGait gait)
    {
        switch (gait)
        {
            case LocomotionGait.Sprint:
                if (_animation.HasClip(AnimationKey.Sprint))
                    return AnimationKey.Sprint;
                if (!_loggedMissingSprint)
                {
                    Debug.LogError("LocomotionService: AnimationProfile 未绑定 Sprint Clip，暂用 Run。");
                    _loggedMissingSprint = true;
                }

                return AnimationKey.Run;
            case LocomotionGait.Run:
                return AnimationKey.Run;
            default:
                return AnimationKey.Walk;
        }
    }

    bool IsStartFinished()
    {
        float gate = _profile != null ? _profile.StartToGaitNormalized : 1f;
        if (gate < 0.999f && _animation.NormalizedTime >= gate)
            return true;
        return IsCurrentPhaseClipFinished();
    }

    bool IsCurrentPhaseClipFinished() => _animation.HasFinishedCurrent;
}

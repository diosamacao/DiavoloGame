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

    LocomotionPhase _phase = LocomotionPhase.Idle;
    LocomotionGait _gait = LocomotionGait.Walk;
    Vector3 _pivotTargetDirection = Vector3.forward;
    /// <summary>进入 Pivot 瞬间的根朝向；含 Y 转向的 Clip 播放期间锁在此方向，避免与代码转根双重叠加。</summary>
    Vector3 _pivotEnterFacing = Vector3.forward;
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
    }

    /// <summary>进入顶层 Locomotion；相位从 Idle 起，保留落脚记录。</summary>
    public void Enter()
    {
        _phase = LocomotionPhase.Idle;
        _runHoldSeconds = 0f;
        _gaitInputGapSeconds = 0f;
        _footCycle.Unfreeze();
        _footCycle.SetMarkers(System.Array.Empty<FootPlantMarker>());
    }

    /// <summary>离开顶层 Locomotion（进 Action 等）；停止落脚采样与派发。</summary>
    public void Exit()
    {
        _footCycle.Freeze();
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
        LocomotionMotorCommand command = BuildMotorCommand(snapshot);
        _motor.ApplyLocomotion(command, deltaTime);
        PlayPhaseAnimation();
        _footstepPlayer.PlayIfPlanted(_footCycle.PlantedThisFrame);
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

        // 1) Start / Pivot 松输入 → 立刻 Stop（起步秒停仍立即；Pivot 结束帧用锁存，见步骤 7）
        if (!hasMove && _phase == LocomotionPhase.Start)
        {
            EnterStop();
            return;
        }

        if (!hasMove && _phase == LocomotionPhase.PivotTurn && !IsCurrentPhaseClipFinished())
        {
            EnterStop();
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

        // 3) Stop：可取消 → Start；播完 → Idle / Start
        if (_phase == LocomotionPhase.Stop)
        {
            float norm = _animation.NormalizedTime;
            float cancelNorm = _profile != null ? _profile.StopCancelNormalized : 0.4f;
            if (hasMove && norm < cancelNorm)
            {
                EnterStart();
                return;
            }

            if (IsCurrentPhaseClipFinished())
            {
                if (hasMove)
                    EnterStart();
                else
                    EnterIdle();
            }

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

        EnterStop();
        if (_phase != LocomotionPhase.Stop)
            return;

        // EnterStop 已 ResetPlayback；先播急停再转根，避免与转身末帧叠姿态
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
        _footCycle.Freeze();
        _footCycle.SetMarkers(System.Array.Empty<FootPlantMarker>());
    }

    void EnterStart()
    {
        _runHoldSeconds = 0f;
        _gaitInputGapSeconds = 0f;
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
    }

    void EnterStop()
    {
        _runHoldSeconds = 0f;
        _gaitInputGapSeconds = 0f;
        _pivotMoveLatched = false;
        FootSide foot = _footCycle.CaptureForStop();
        _stopKey = foot == FootSide.Left ? AnimationKey.StopL : AnimationKey.StopR;
        if (!_animation.HasClip(_stopKey))
        {
            AnimationKey fallback = _stopKey == AnimationKey.StopL ? AnimationKey.StopR : AnimationKey.StopL;
            if (_animation.HasClip(fallback))
            {
                _stopKey = fallback;
            }
            else
            {
                if (!_loggedMissingStop)
                {
                    Debug.LogError("LocomotionService: AnimationProfile 未绑定 StopL/StopR，急停直接回 Idle。");
                    _loggedMissingStop = true;
                }

                EnterIdle();
                return;
            }
        }

        _phase = LocomotionPhase.Stop;
        _footCycle.SetMarkers(System.Array.Empty<FootPlantMarker>());
        _animation.ResetPlaybackState();
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
    /// 默认（Clip 含 Y 转向）：全程锁进入朝向，由动画表现转身，结束再对齐输入。
    /// pivotRootFollowsInput 时（Clip 朝前）：对齐 zzzdemo ReturnRun，前段 Hold、其后慢跟输入。
    /// </summary>
    LocomotionMotorCommand BuildPivotMotorCommand(in LocomotionInputSnapshot snapshot)
    {
        bool applyMove = snapshot.HasMoveInput;
        bool rootFollows = _profile != null && _profile.PivotRootFollowsInput;

        if (!rootFollows)
        {
            // 每帧钉回进入朝向，防止其它系统改根
            _motor.FaceWorldDirection(_pivotEnterFacing);
            return new LocomotionMotorCommand(
                applyMove,
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
                applyMove,
                LocomotionRotationMode.Hold,
                _pivotEnterFacing,
                LocomotionGait.Sprint);
        }

        return new LocomotionMotorCommand(
            applyMove,
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

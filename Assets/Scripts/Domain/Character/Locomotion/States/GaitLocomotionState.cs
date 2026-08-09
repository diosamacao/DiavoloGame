using UnityEngine;

/// <summary>
/// 稳态走跑冲刺：循环 Clip + FootCycle；宽限后松手 Stop/Idle；
/// Pivot / 升档由 Profile.GaitPolicy 求值；播片经 AnimResolver。
/// </summary>
public sealed class GaitLocomotionState : LocomotionPhaseState
{
    public override LocomotionPhase Id => LocomotionPhase.Gait;

    /// <summary>应用 PendingGait，结束烘焙根位移；可选硬切 Play 与对齐朝向。</summary>
    public override void Enter()
    {
        Context.RootMotionPlayer.End();
        LocomotionGaitPolicy policy = Context.Profile != null
            ? Context.Profile.GaitPolicy
            : new LocomotionGaitPolicy();
        LocomotionGait gait = policy.ClampGait(Context.PendingGait);
        if (gait != LocomotionGait.Sprint)
            Context.RunHoldSeconds = 0f;

        Context.SetGait(gait);
        Context.FootCycle.Unfreeze();

        if (Context.PendingGaitHardCutPlay)
        {
            Context.Animation.ResetPlaybackState();
            Context.Animation.Play(Context.ResolveLocomotionAnimationKey(), 0f);
            Context.PendingGaitHardCutPlay = false;
        }

        if (Context.PendingGaitFaceDirection.sqrMagnitude > 0.0001f)
        {
            Context.Motor.FaceWorldDirection(Context.PendingGaitFaceDirection);
            Context.Motor.ResetRotationDamping();
            Context.PendingGaitFaceDirection = Vector3.zero;
        }
    }

    /// <summary>松手宽限 / Pivot / 步态升降档。</summary>
    public override void Tick(float deltaTime)
    {
        LocomotionInputSnapshot snapshot = Context.FrameSnapshot;
        bool hasMove = Context.HasMeaningfulMove(snapshot);
        if (hasMove)
            Context.GaitInputGapSeconds = 0f;

        if (!hasMove)
        {
            Context.GaitInputGapSeconds += deltaTime;
            float grace = Context.Profile != null ? Context.Profile.GaitInputGapGraceSeconds : 0.15f;
            if (Context.GaitInputGapSeconds < grace)
                return;

            Context.GaitInputGapSeconds = 0f;
            float stopMin = Context.Motor.RunSpeed
                * (Context.Profile != null ? Context.Profile.StopMinSpeedFactor : 0.5f);
            if (snapshot.PlanarSpeed >= stopMin || LocomotionContext.IsRunTier(Context.Gait))
                Context.GoStop(fromStart: false);
            else
                Context.RequestPhase(LocomotionPhase.Idle, force: true);
            return;
        }

        if (CanEnterPivot(snapshot))
        {
            Context.TryGoPivot(snapshot.WorldMoveDirection);
            return;
        }

        UpdateGaitWhileMoving(snapshot.Magnitude, deltaTime);
    }

    /// <summary>跟输入移动；播当前步态循环并采样落脚。</summary>
    public override void ExecuteFrame(float deltaTime)
    {
        Context.FootCycle.Unfreeze();
        Context.FootCycle.Tick(Context.Animation.NormalizedTime);
        AnimationKey key = Context.ResolveLocomotionAnimationKey();
        // 降档用短淡入（非 0）：既避免长时间停在 Run，又比硬切自然
        if (Context.PendingGaitHardCutPlay)
        {
            float fade = Context.Profile != null ? Context.Profile.InterruptFadeDuration : 0.08f;
            Context.Animation.Play(key, fade);
            Context.PendingGaitHardCutPlay = false;
        }
        else
            Context.Animation.Play(key);

        Context.Motor.ApplyLocomotion(
            new LocomotionMotorCommand(
                true,
                Context.ResolveGaitRotationMode(),
                Vector3.zero,
                Context.Gait),
            deltaTime);
        Context.FootstepPlayer.PlayIfPlanted(Context.FootCycle.PlantedThisFrame);
    }

    /// <summary>Policy 允许且与输入夹角超过阈值时进折返。</summary>
    bool CanEnterPivot(in LocomotionInputSnapshot snapshot)
    {
        LocomotionGaitPolicy policy = Context.Profile != null
            ? Context.Profile.GaitPolicy
            : new LocomotionGaitPolicy();
        if (!policy.AllowsPivot(Context.Gait))
            return false;
        if (snapshot.WorldMoveDirection.sqrMagnitude < 0.001f)
            return false;

        float pivotAngle = Context.Profile != null ? Context.Profile.PivotAngleDegrees : 135f;
        Vector3 facing = Context.Root.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f)
            return false;

        float angleCurrent = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(snapshot.WorldMoveDirection.x, snapshot.WorldMoveDirection.z)
            * Mathf.Rad2Deg;
        float yawError = Mathf.Abs(Mathf.DeltaAngle(angleCurrent, targetAngle));
        return yawError > pivotAngle;
    }

    /// <summary>委托 GaitPolicy.Evaluate 升档。</summary>
    void UpdateGaitWhileMoving(float magnitude, float deltaTime)
    {
        LocomotionGaitPolicy policy = Context.Profile != null
            ? Context.Profile.GaitPolicy
            : new LocomotionGaitPolicy();

        GaitPolicyResult result = policy.Evaluate(new GaitPolicyInput(
            Context.Gait,
            magnitude,
            Context.Motor.RunThreshold,
            deltaTime,
            Context.RunHoldSeconds));

        Context.RunHoldSeconds = result.RunHoldSeconds;
        if (result.NextGait == Context.Gait)
            return;

        // Run/Sprint → Walk：标记短淡入（InterruptFade），不用 0 硬切
        bool downgradeToWalk = result.NextGait == LocomotionGait.Walk
            && LocomotionContext.IsRunTier(Context.Gait);
        Context.SetGait(result.NextGait);
        if (downgradeToWalk)
            Context.PendingGaitHardCutPlay = true;
    }
}

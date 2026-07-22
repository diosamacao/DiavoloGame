using UnityEngine;

/// <summary>
/// 稳态走跑冲刺：循环 Clip + FootCycle；宽限后松手 Stop/Idle；
/// Sprint 大角度进 Pivot；内部处理 Walk/Run/Sprint 升档。
/// </summary>
public sealed class GaitLocomotionState : LocomotionPhaseState
{
    public override LocomotionPhase Id => LocomotionPhase.Gait;

    /// <summary>应用 PendingGait，结束烘焙根位移；可选硬切 Play 与对齐朝向。</summary>
    public override void Enter()
    {
        Context.RootMotionPlayer.End();
        LocomotionGait gait = Context.PendingGait;
        if (gait != LocomotionGait.Sprint)
            Context.RunHoldSeconds = 0f;

        Context.SetGait(gait);
        Context.FootCycle.Unfreeze();

        if (Context.PendingGaitHardCutPlay)
        {
            Context.Animation.ResetPlaybackState();
            Context.Animation.Play(Context.ResolveGaitAnimationKey(Context.Gait), 0f);
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
        Context.Animation.Play(Context.ResolveGaitAnimationKey(Context.Gait));
        Context.Motor.ApplyLocomotion(
            new LocomotionMotorCommand(
                true,
                LocomotionRotationMode.FollowInput,
                Vector3.zero,
                Context.Gait),
            deltaTime);
        Context.FootstepPlayer.PlayIfPlanted(Context.FootCycle.PlantedThisFrame);
    }

    /// <summary>仅 Sprint 且与输入夹角超过阈值时允许折返。</summary>
    bool CanEnterPivot(in LocomotionInputSnapshot snapshot)
    {
        if (Context.Gait != LocomotionGait.Sprint)
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

    /// <summary>跑输入持续累计；满时长 Run→Sprint；降到走输入则回 Walk。</summary>
    void UpdateGaitWhileMoving(float magnitude, float deltaTime)
    {
        bool wantRunTier = magnitude > Context.Motor.RunThreshold;
        if (!wantRunTier)
        {
            Context.RunHoldSeconds = 0f;
            if (Context.Gait != LocomotionGait.Walk)
                Context.SetGait(LocomotionGait.Walk);
            return;
        }

        if (Context.Gait == LocomotionGait.Walk)
        {
            Context.RunHoldSeconds = 0f;
            Context.SetGait(LocomotionGait.Run);
            return;
        }

        if (Context.Gait == LocomotionGait.Run)
        {
            Context.RunHoldSeconds += deltaTime;
            float need = Context.Profile != null ? Context.Profile.SprintAfterRunSeconds : 3f;
            if (Context.RunHoldSeconds >= need)
            {
                Context.RunHoldSeconds = 0f;
                Context.SetGait(LocomotionGait.Sprint);
            }
        }
    }
}

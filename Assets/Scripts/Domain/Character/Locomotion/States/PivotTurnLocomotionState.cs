using UnityEngine;

/// <summary>
/// 折返相位：烘焙根位移 + 短锁根；解锁后叠加输入相对初始折返方向的偏移；
/// 松手进 Stop；播完直入 Sprint 或急停。
/// </summary>
public sealed class PivotTurnLocomotionState : LocomotionPhaseState
{
    public override LocomotionPhase Id => LocomotionPhase.PivotTurn;

    /// <summary>锁进入朝向、硬切 PivotTurn、开始烘焙根位移会话。</summary>
    public override void Enter()
    {
        Context.PivotElapsedSeconds = 0f;
        Context.PivotMoveLatched = true;
        Context.PivotInitialTargetDirection = Context.PivotTargetDirection;

        Vector3 enterFacing = Context.Root.forward;
        enterFacing.y = 0f;
        if (enterFacing.sqrMagnitude < 0.0001f)
            enterFacing = Vector3.forward;
        else
            enterFacing.Normalize();
        Context.PivotEnterFacing = enterFacing;

        Context.Motor.ResetRotationDamping();
        Context.Motor.FaceWorldDirection(Context.PivotEnterFacing);
        Context.FootCycle.Freeze();
        Context.FootCycle.SetMarkers(System.Array.Empty<FootPlantMarker>());
        Context.Animation.ResetPlaybackState();
        // 转身起手硬切，避免与 Sprint CrossFade 把朝向混花
        Context.Animation.Play(AnimationKey.PivotTurn, 0f);
        Context.RootMotionPlayer.Begin(
            AnimationKey.PivotTurn,
            Quaternion.LookRotation(Context.PivotEnterFacing));
    }

    /// <summary>刷新目标；松手 Stop；播完 Finish→Sprint/Stop。</summary>
    public override void Tick(float deltaTime)
    {
        LocomotionInputSnapshot snapshot = Context.FrameSnapshot;
        bool hasMove = Context.HasMeaningfulMove(snapshot);
        if (hasMove)
            Context.GaitInputGapSeconds = 0f;

        if (hasMove && snapshot.WorldMoveDirection.sqrMagnitude > 0.001f)
        {
            Context.PivotTargetDirection = snapshot.WorldMoveDirection.normalized;
            Context.PivotMoveLatched = true;
        }

        if (!hasMove && !Context.IsCurrentPhaseClipFinished())
        {
            Context.GoStop(fromStart: false, preferredFacing: Context.PivotTargetDirection);
            return;
        }

        if (Context.IsCurrentPhaseClipFinished())
            FinishPivotTurn(snapshot, hasMove);
    }

    /// <summary>推进锁根计时；烘焙位移；解锁后 PivotTarget 旋转。</summary>
    public override void ExecuteFrame(float deltaTime)
    {
        Context.PivotElapsedSeconds += deltaTime;
        Context.FootCycle.Freeze();
        Context.Animation.Play(AnimationKey.PivotTurn);

        LocomotionMotorCommand command = BuildPivotMotorCommand();
        if (Context.RootMotionPlayer.IsActive)
            Context.ApplyBakedRootMotion(LocomotionPhase.PivotTurn, in command, deltaTime);
        else
            Context.Motor.ApplyLocomotion(command, deltaTime);
    }

    /// <summary>
    /// 转身结束：先硬切 Sprint/Stop，再对齐朝向，避免末帧本地 180° 与新根朝向叠闪。
    /// </summary>
    void FinishPivotTurn(in LocomotionInputSnapshot snapshot, bool hasMove)
    {
        Vector3 faceDir = snapshot.WorldMoveDirection.sqrMagnitude > 0.001f
            ? snapshot.WorldMoveDirection
            : Context.PivotTargetDirection;

        bool resumeSprint = Context.PivotMoveLatched || hasMove;
        Context.PivotMoveLatched = false;
        Context.RootMotionPlayer.End();

        if (resumeSprint)
        {
            Context.GoGait(LocomotionGait.Sprint, hardCutPlay: true, faceDirection: faceDir);
            return;
        }

        Context.GoStop(fromStart: false, preferredFacing: faceDir, hardCut: true);
    }

    /// <summary>
    /// Pivot 位移由烘焙根运动负责时关闭输入推移。
    /// 起手锁根；解锁后叠加相对初始折返输入的方向差。
    /// </summary>
    LocomotionMotorCommand BuildPivotMotorCommand()
    {
        float unlockSeconds = Context.Profile != null ? Context.Profile.PivotInputUnlockSeconds : 0.08f;
        float pivotSmooth = Context.Profile != null ? Context.Profile.PivotRotationSmoothTime : 0.5f;
        if (Context.PivotElapsedSeconds < unlockSeconds)
        {
            Context.Motor.FaceWorldDirection(Context.PivotEnterFacing);
            return new LocomotionMotorCommand(
                false,
                LocomotionRotationMode.Hold,
                Context.PivotEnterFacing,
                LocomotionGait.Sprint);
        }

        return new LocomotionMotorCommand(
            false,
            LocomotionRotationMode.PivotTarget,
            Context.ResolvePivotSteeringRootDirection(),
            LocomotionGait.Sprint,
            pivotSmooth);
    }
}

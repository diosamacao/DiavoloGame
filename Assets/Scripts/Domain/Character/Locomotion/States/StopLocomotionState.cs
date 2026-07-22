using UnityEngine;

/// <summary>急停相位：StartEnd 或 StopL/R + 烘焙位移；任意时刻可取消回 Start；播完回 Idle。</summary>
public sealed class StopLocomotionState : LocomotionPhaseState
{
    public override LocomotionPhase Id => LocomotionPhase.Stop;

    /// <summary>对齐急停朝向、播 StopKey、开始烘焙根位移。</summary>
    public override void Enter()
    {
        Context.RunHoldSeconds = 0f;
        Context.GaitInputGapSeconds = 0f;
        Context.PivotMoveLatched = false;

        Context.Motor.FaceWorldDirection(Context.StopEnterFacing);
        Context.FootCycle.SetMarkers(System.Array.Empty<FootPlantMarker>());
        Context.Animation.ResetPlaybackState();

        bool hardCut = Context.StopPlayHardCut;
        Context.StopPlayHardCut = false;
        float fade = hardCut
            ? 0f
            : (Context.Profile != null ? Context.Profile.InterruptFadeDuration : 0.08f);
        Context.Animation.Play(Context.StopKey, fade);
        if (hardCut)
            Context.Motor.ResetRotationDamping();
        Context.RootMotionPlayer.Begin(
            Context.StopKey,
            Quaternion.LookRotation(Context.StopEnterFacing));
    }

    /// <summary>有输入 → Start；播完 → Idle。</summary>
    public override void Tick(float deltaTime)
    {
        if (Context.HasMeaningfulMove(Context.FrameSnapshot))
        {
            Context.RequestPhase(LocomotionPhase.Start, force: true);
            return;
        }

        if (Context.IsCurrentPhaseClipFinished())
            Context.RequestPhase(LocomotionPhase.Idle, force: true);
    }

    /// <summary>烘焙急停位移；锁朝向；维持 Stop Clip。</summary>
    public override void ExecuteFrame(float deltaTime)
    {
        Context.FootCycle.Freeze();
        float interrupt = Context.Profile != null ? Context.Profile.InterruptFadeDuration : 0.08f;
        Context.Animation.Play(Context.StopKey, interrupt);

        var command = new LocomotionMotorCommand(
            false,
            LocomotionRotationMode.Hold,
            Context.StopEnterFacing,
            LocomotionGait.Walk);

        if (Context.RootMotionPlayer.IsActive)
            Context.ApplyBakedRootMotion(LocomotionPhase.Stop, in command, deltaTime);
        else
            Context.Motor.ApplyLocomotion(command, deltaTime);
    }
}

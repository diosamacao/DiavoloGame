using UnityEngine;

/// <summary>起步相位：播 Start；播完进 Gait；松输入进 Stop(StartEnd)。</summary>
public sealed class StartLocomotionState : LocomotionPhaseState
{
    public override LocomotionPhase Id => LocomotionPhase.Start;

    /// <summary>缺 Start Clip 时直接进 Gait；否则解冻落脚并重置播放。</summary>
    public override void Enter()
    {
        Context.RunHoldSeconds = 0f;
        Context.GaitInputGapSeconds = 0f;
        Context.RootMotionPlayer.End();
        if (!Context.Animation.HasClip(AnimationKey.Start))
        {
            if (!Context.LoggedMissingStart)
            {
                Debug.LogError("LocomotionStateMachine: AnimationProfile 未绑定 Start Clip，已跳过起步直接进 Gait。");
                Context.LoggedMissingStart = true;
            }

            Context.GoGait(Context.ResolveInitialGait(Context.Input.MoveMagnitude));
            return;
        }

        Context.FootCycle.Unfreeze();
        Context.FootCycle.SetMarkers(Context.GetMarkersForPhase(LocomotionPhase.Start));
        Context.Animation.ResetPlaybackState();
    }

    /// <summary>松输入 → Stop；播完 → Gait(Walk|Run)。</summary>
    public override void Tick(float deltaTime)
    {
        LocomotionInputSnapshot snapshot = Context.FrameSnapshot;
        if (!Context.HasMeaningfulMove(snapshot))
        {
            Context.GoStop(fromStart: true);
            return;
        }

        if (Context.IsStartFinished())
            Context.GoGait(Context.ResolveInitialGait(snapshot.Magnitude));
    }

    /// <summary>跟输入移动；推进落脚与 Start 动画。</summary>
    public override void ExecuteFrame(float deltaTime)
    {
        Context.FootCycle.Unfreeze();
        Context.FootCycle.Tick(Context.Animation.NormalizedTime);
        Context.Animation.Play(AnimationKey.Start);
        Context.Motor.ApplyLocomotion(
            new LocomotionMotorCommand(
                true,
                LocomotionRotationMode.FollowInput,
                Vector3.zero,
                Context.ResolveInitialGait(Context.FrameSnapshot.Magnitude)),
            deltaTime);
        Context.FootstepPlayer.PlayIfPlanted(Context.FootCycle.PlantedThisFrame);
    }
}

using UnityEngine;

/// <summary>静止相位：播 Idle；有有效移动输入时必经 Start。</summary>
public sealed class IdleLocomotionState : LocomotionPhaseState
{
    public override LocomotionPhase Id => LocomotionPhase.Idle;

    /// <summary>清步态计时，结束烘焙根位移并冻结落脚。</summary>
    public override void Enter()
    {
        Context.Gait = LocomotionGait.Walk;
        Context.RunHoldSeconds = 0f;
        Context.GaitInputGapSeconds = 0f;
        Context.RootMotionPlayer.End();
        Context.FootCycle.Freeze();
        Context.FootCycle.SetMarkers(System.Array.Empty<FootPlantMarker>());
    }

    /// <summary>有移动 → Start。</summary>
    public override void Tick(float deltaTime)
    {
        if (Context.HasMeaningfulMove(Context.FrameSnapshot))
            Context.RequestPhase(LocomotionPhase.Start);
    }

    /// <summary>保持 Idle 动画与静止 Motor 命令。</summary>
    public override void ExecuteFrame(float deltaTime)
    {
        Context.Animation.SetSpeed(1f);
        Context.Animation.Play(AnimationKey.Idle);
        Context.Motor.ApplyLocomotion(
            new LocomotionMotorCommand(
                false,
                LocomotionRotationMode.Hold,
                LocomotionGait.Walk),
            deltaTime);
    }
}

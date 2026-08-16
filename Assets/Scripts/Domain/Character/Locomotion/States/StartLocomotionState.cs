using UnityEngine;

/// <summary>
/// 起步相位：AnimSet.ResolveStart 闩定 Key/Gait/Cardinal；升档/降档看 ActiveStartGait，不认 Key 族。
/// </summary>
public sealed class StartLocomotionState : LocomotionPhaseState
{
    public override LocomotionPhase Id => LocomotionPhase.Start;

    /// <summary>缺起步 Clip 时直接进 Gait；否则按进入瞬间输入锁定起步槽。</summary>
    public override void Enter()
    {
        Context.RunHoldSeconds = 0f;
        Context.GaitInputGapSeconds = 0f;
        Context.RootMotionPlayer.End();
        if (!Context.HasAnyStartClip())
        {
            if (!Context.LoggedMissingStart)
            {
                Debug.LogError(
                    "LocomotionStateMachine: AnimSet/AnimationProfile 无可用起步 Clip，已跳过起步直接进 Gait。");
                Context.LoggedMissingStart = true;
            }

            Context.GoGait(Context.ResolveInitialGait(Context.Input.MoveMagnitude));
            return;
        }

        // 进入瞬间闩 cardinal + 步态档，Start 中途微抖不换片
        Context.ResolveAndLatchStartKey(Context.Input.MoveMagnitude, Context.Input.MoveIntent);
        Context.FootCycle.Unfreeze();
        Context.FootCycle.SetMarkers(Context.GetMarkersForPhase(LocomotionPhase.Start));
        Context.Animation.ResetPlaybackState();
    }

    /// <summary>松输入 → Stop；走起步升跑 → Run Gait；跑起步降走 → 重闩；播完 → Gait。</summary>
    public override void Tick(float deltaTime)
    {
        LocomotionInputSnapshot snapshot = Context.FrameSnapshot;
        // 松手：从起步进急停（用 StartEnd 片）
        if (!Context.HasMeaningfulMove(snapshot))
        {
            Context.GoStop(fromStart: true);
            return;
        }

        // 走档起步且输入已属 Run：直入 Run 循环
        if (TryPromoteWalkStartToRunGait(in snapshot))
            return;

        // 跑档起步降走：重闩走起步，避免播完才降档
        TryRelatchAfterGaitDowngrade(in snapshot);

        // 起步 Clip 播完：按当前输入进对应 Gait
        if (Context.IsStartFinished())
            Context.GoGait(Context.ResolveInitialGait(snapshot.Magnitude));
    }

    /// <summary>跟 FacingMode 移动；推进落脚与闩定起步动画。</summary>
    public override void ExecuteFrame(float deltaTime)
    {
        Context.FootCycle.Unfreeze();
        Context.FootCycle.Tick(Context.Animation.NormalizedTime);
        Context.Animation.Play(Context.ActiveStartKey);
        Context.Motor.ApplyLocomotion(
            new LocomotionMotorCommand(
                true,
                Context.ResolveGaitRotationMode(),
                Context.ResolveInitialGait(Context.FrameSnapshot.Magnitude)),
            deltaTime);
        Context.FootstepPlayer.PlayIfPlanted(Context.FootCycle.PlantedThisFrame);
    }

    /// <summary>闩的是走档起步且输入已属 Run → 直入 Run 循环。</summary>
    bool TryPromoteWalkStartToRunGait(in LocomotionInputSnapshot snapshot)
    {
        if (Context.ActiveStartGait != LocomotionGait.Walk)
            return false;
        if (Context.ResolveInitialGait(snapshot.Magnitude) != LocomotionGait.Run)
            return false;

        Context.GoGait(LocomotionGait.Run);
        return true;
    }

    /// <summary>闩的是跑档起步且输入已属 Walk → 重选走起步（短淡入）。</summary>
    void TryRelatchAfterGaitDowngrade(in LocomotionInputSnapshot snapshot)
    {
        if (Context.ActiveStartGait != LocomotionGait.Run
            && Context.ActiveStartGait != LocomotionGait.Sprint)
            return;
        if (Context.ResolveInitialGait(snapshot.Magnitude) != LocomotionGait.Walk)
            return;

        AnimationKey next = Context.ResolveAndLatchStartKey(snapshot.Magnitude, snapshot.MoveIntent);
        float fade = Context.Profile != null ? Context.Profile.InterruptFadeDuration : 0.08f;
        Context.Animation.ResetPlaybackState();
        Context.Animation.Play(next, fade);
    }
}

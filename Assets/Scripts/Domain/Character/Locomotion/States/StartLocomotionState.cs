using UnityEngine;

/// <summary>起步相位：按步态与 Cardinal 播 WalkStartLeft/Right/WalkStart/Start；朝向经 FacingMode。</summary>
public sealed class StartLocomotionState : LocomotionPhaseState
{
    public override LocomotionPhase Id => LocomotionPhase.Start;

    /// <summary>缺起步 Clip 时直接进 Gait；否则按进入瞬间输入锁定 ActiveStartKey。</summary>
    public override void Enter()
    {
        Context.RunHoldSeconds = 0f;
        Context.GaitInputGapSeconds = 0f;
        Context.RootMotionPlayer.End();
        if (!DefaultLocomotionAnimResolver.HasAnyStartClip(Context.Animation))
        {
            if (!Context.LoggedMissingStart)
            {
                Debug.LogError(
                    "LocomotionStateMachine: AnimationProfile 未绑定 WalkStart(Left/Right)/Start Clip，已跳过起步直接进 Gait。");
                Context.LoggedMissingStart = true;
            }

            Context.GoGait(Context.ResolveInitialGait(Context.Input.MoveMagnitude));
            return;
        }

        // 进入瞬间锁左右起步，避免 Start 中途换向切片
        Context.ResolveAndLatchStartKey(Context.Input.MoveMagnitude, Context.Input.MoveIntent);
        Context.FootCycle.Unfreeze();
        Context.FootCycle.SetMarkers(Context.GetMarkersForPhase(LocomotionPhase.Start));
        Context.Animation.ResetPlaybackState();
    }

    /// <summary>松输入 → Stop；走起步升跑 → 直入 Run Gait；跑起步降走 → 重闩；播完 → Gait。</summary>
    public override void Tick(float deltaTime)
    {
        LocomotionInputSnapshot snapshot = Context.FrameSnapshot;
        if (!Context.HasMeaningfulMove(snapshot))
        {
            Context.GoStop(fromStart: true);
            return;
        }

        // 对峙 WalkStart* 中玩家拉开：幅度进 Run 则立刻进跑循环，避免继续播走起步却以跑速位移
        if (TryPromoteWalkStartToRunGait(in snapshot))
            return;

        // 追击起手后立刻对峙：丢掉 RunStart，短淡入 WalkStart*
        TryRelatchWalkStartAfterDowngrade(in snapshot);

        if (Context.IsStartFinished())
            Context.GoGait(Context.ResolveInitialGait(snapshot.Magnitude));
    }

    /// <summary>跟配置旋转模式移动；推进落脚与起步动画。</summary>
    public override void ExecuteFrame(float deltaTime)
    {
        Context.FootCycle.Unfreeze();
        Context.FootCycle.Tick(Context.Animation.NormalizedTime);
        Context.Animation.Play(Context.ActiveStartKey);
        Context.Motor.ApplyLocomotion(
            new LocomotionMotorCommand(
                true,
                Context.ResolveGaitRotationMode(),
                Vector3.zero,
                Context.ResolveInitialGait(Context.FrameSnapshot.Magnitude)),
            deltaTime);
        Context.FootstepPlayer.PlayIfPlanted(Context.FootCycle.PlantedThisFrame);
    }

    /// <summary>Walk 起步族 + 跑输入 → 直接进 Run Gait。</summary>
    bool TryPromoteWalkStartToRunGait(in LocomotionInputSnapshot snapshot)
    {
        if (!IsWalkStartFamily(Context.ActiveStartKey))
            return false;
        if (Context.ResolveInitialGait(snapshot.Magnitude) != LocomotionGait.Run)
            return false;

        Context.GoGait(LocomotionGait.Run);
        return true;
    }

    /// <summary>闩的是跑起步且输入已属 Walk 时重选走起步（短淡入，非硬切）。</summary>
    void TryRelatchWalkStartAfterDowngrade(in LocomotionInputSnapshot snapshot)
    {
        if (Context.ActiveStartKey != AnimationKey.Start)
            return;
        if (Context.ResolveInitialGait(snapshot.Magnitude) != LocomotionGait.Walk)
            return;

        AnimationKey next = Context.ResolveAndLatchStartKey(snapshot.Magnitude, snapshot.MoveIntent);
        float fade = Context.Profile != null ? Context.Profile.InterruptFadeDuration : 0.08f;
        Context.Animation.ResetPlaybackState();
        Context.Animation.Play(next, fade);
    }

    static bool IsWalkStartFamily(AnimationKey key) =>
        key == AnimationKey.WalkStart
        || key == AnimationKey.WalkStartLeft
        || key == AnimationKey.WalkStartRight;
}

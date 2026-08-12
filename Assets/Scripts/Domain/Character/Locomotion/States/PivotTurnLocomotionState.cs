using UnityEngine;

/// <summary>
/// 折返相位两段式：AnimAuth（烘焙根位移+偏航）→ InputAuth（与 Gait 相同的 FollowInput/FaceCamera）。
/// 松手进 Stop；播完直入 Sprint 或急停。
/// </summary>
public sealed class PivotTurnLocomotionState : LocomotionPhaseState
{
    /// <summary>已切入输入接管段；Enter 时清零。</summary>
    bool _inputAuth;

    public override LocomotionPhase Id => LocomotionPhase.PivotTurn;

    /// <summary>锁进入朝向为烘焙基、硬切 PivotTurn、开始根位移会话。</summary>
    public override void Enter()
    {
        _inputAuth = false;
        Context.PivotMoveLatched = true;

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

        // 无有效烘焙轨时整段走 InputAuth，避免卡在无位移 AnimAuth
        if (!Context.RootMotionPlayer.IsActive)
            EnterInputAuth();
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

    /// <summary>AnimAuth 吃烘焙；过 handoff 后 End 根运动并 FollowInput 推移。</summary>
    public override void ExecuteFrame(float deltaTime)
    {
        Context.FootCycle.Freeze();
        Context.Animation.Play(AnimationKey.PivotTurn);

        if (!_inputAuth)
        {
            float handoff = Context.Profile != null
                ? Context.Profile.PivotAnimAuthNormalized
                : 0.5f;
            // 用动画归一化时间切段（方案定案）；位移仍由 RootMotionPlayer 逻辑帧消费
            if (Context.Animation.NormalizedTime >= handoff)
                EnterInputAuth();
        }

        if (!_inputAuth)
        {
            Context.ApplyBakedRootMotion(LocomotionPhase.PivotTurn, deltaTime);
            return;
        }

        // InputAuth：与稳态 Gait 同一 ApplyLocomotion 语义（朝向追 wish、位移沿朝向）
        Context.Motor.ApplyLocomotion(
            new LocomotionMotorCommand(
                true,
                Context.ResolveGaitRotationMode(),
                LocomotionGait.Sprint),
            deltaTime);
    }

    /// <summary>一次性切入输入段：结束烘焙会话，平滑阻尼从当前朝向重新积分。</summary>
    void EnterInputAuth()
    {
        if (_inputAuth)
            return;

        _inputAuth = true;
        Context.RootMotionPlayer.End();
        // 清零 SmoothDamp 速度，避免 AnimAuth 期间残留角速度在交接后过冲
        Context.Motor.ResetRotationDamping();
    }

    /// <summary>
    /// 转身结束：硬切 Sprint Clip，但朝向不硬对齐 wish——保留 InputAuth 已追到的朝向，
    /// 避免 FaceWorldDirection(wish) 后 L-DIR5 相机基座再把 wish 拉开。
    /// </summary>
    void FinishPivotTurn(in LocomotionInputSnapshot snapshot, bool hasMove)
    {
        Vector3 stopFacing = snapshot.WorldMoveDirection.sqrMagnitude > 0.001f
            ? snapshot.WorldMoveDirection
            : Context.PivotTargetDirection;

        bool resumeSprint = Context.PivotMoveLatched || hasMove;
        Context.PivotMoveLatched = false;
        Context.RootMotionPlayer.End();
        _inputAuth = false;

        if (resumeSprint)
        {
            // 不传 faceDirection：Gait.Enter 不再 FaceWorldDirection(wish)
            Context.GoGait(LocomotionGait.Sprint, hardCutPlay: true);
            return;
        }

        Context.GoStop(fromStart: false, preferredFacing: stopFacing, hardCut: true);
    }
}

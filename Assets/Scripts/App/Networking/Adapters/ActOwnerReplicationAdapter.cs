using System;
using UnityEngine;

/// <summary>ACT Owner 复制适配器：维护预测历史，并应用 HP、Action 与 Locomotion 权威和解。</summary>
public sealed class ActOwnerReplicationAdapter
{
    readonly ActContentRegistry _content;
    PredictedActionAckQueue _actionAck = new();
    PredictedLocomotionDriver _driver;
    SimActorId _ownerActorId;
    InputFrameBuffer _inputFrames;
    bool _hasSelfSnapshot;
    int _selfHealthMilli = -1;

    /// <summary>创建绑定当前房间动作目录的 Owner 适配器。</summary>
    public ActOwnerReplicationAdapter(ActContentRegistry content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    /// <summary>首份 Owner 快照已到达且预测 Driver 已完成绑定时为 true。</summary>
    public bool CanPredict => _hasSelfSnapshot && _driver != null;

    /// <summary>Owner 最近一次权威生命值；尚无快照时为 -1。</summary>
    public int SelfHealthMilli => _selfHealthMilli;

    /// <summary>尚未被权威确认的位移与动作预测总数。</summary>
    public int PendingCount => (_driver?.PendingCount ?? 0) + _actionAck.PendingCount;

    /// <summary>走跑纠偏 snap / replay 计数；尚未绑 Driver 时为 0。</summary>
    public int LocomotionSnapCount => _driver != null ? _driver.Metrics.SnapCount : 0;

    /// <summary>走跑纠偏累计重放命令数。</summary>
    public int LocomotionReplayCount => _driver != null ? _driver.Metrics.ReplayCount : 0;

    /// <summary>Session Join 后绑定已映射的 Owner SimActorId 与本地输入历史。</summary>
    public void BeginSession(SimActorId ownerActorId, InputFrameBuffer inputFrames)
    {
        if (!ownerActorId.IsValid)
            throw new ArgumentException("Owner 必须绑定有效 SimActorId。", nameof(ownerActorId));
        _ownerActorId = ownerActorId;
        _inputFrames = inputFrames ?? throw new ArgumentNullException(nameof(inputFrames));
        _driver = null;
        _actionAck = new PredictedActionAckQueue();
        _hasSelfSnapshot = false;
        _selfHealthMilli = -1;
    }

    /// <summary>
    /// 切换当前受控槽身份并丢弃上一槽预测历史；输入历史由座位继续共用。
    /// </summary>
    public void SetActiveOwnerActor(SimActorId ownerActorId)
    {
        if (!ownerActorId.IsValid)
            throw new ArgumentException("Active Owner 必须绑定有效 SimActorId。", nameof(ownerActorId));
        if (_ownerActorId == ownerActorId)
            return;

        _ownerActorId = ownerActorId;
        _driver = null;
        _actionAck = new PredictedActionAckQueue();
        _hasSelfSnapshot = false;
    }

    /// <summary>记录本逻辑帧 Autonomous Actor 已执行的动作与位移结果，供后续 ACK/Replay。</summary>
    public void RecordAutonomous(
        CharacterActor actor,
        long frame,
        in InputFrame input)
    {
        if (actor == null || _driver == null)
            return;

        int actionId = ResolveLocalActionId(actor);
        _actionAck.Record(frame, actionId);
        _driver.RecordAutonomous(in input);
    }

    /// <summary>应用 Owner 权威快照：覆盖 HP，并按 appliedHint 执行动作 ACK 与位移和解。吸附/闪避招在权威空闲时不掐。</summary>
    public void ApplySnapshot(
        PlayerController localPlayer,
        in ActorReplicationSnapshot self,
        long appliedHint)
    {
        if (self.Kind != ReplicationActorKind.Player)
            throw new InvalidOperationException("Owner Snapshot Kind 必须为 Player。");
        if (!_ownerActorId.IsValid || self.ActorId != _ownerActorId)
            throw new InvalidOperationException("Owner Snapshot ActorId 与 Session 分配实体不一致。");

        _hasSelfSnapshot = true;
        _selfHealthMilli = self.HealthMilli;
        EnsurePredictedDriver(localPlayer, in self);
        CharacterActor actor = localPlayer != null ? localPlayer.Actor : null;
        if (actor != null)
            actor.Vitality.ApplyAuthorityHealthMilli(self.HealthMilli);

        // CarryForward hint=0 只覆盖状态，禁止拿旧预测位姿和解当前权威帧。
        if (appliedHint > 0 && actor != null && _driver != null)
        {
            PredictedActionReconcileResult actionResult = _actionAck.Reconcile(
                appliedHint,
                in self);
            ActionMotionReconcileGate.TryReadLocalAction(actor, out ActionDefinition localAction, out _);
            _content.Actions.TryGet(self.ActionId, out ActionDefinition authorityAction);
            if (PredictedActionAckQueue.ShouldStopAutonomousAction(
                    actionResult,
                    in self,
                    ActionMotionReconcileGate.HasCorrectiveDisplacement(localAction)))
            {
                actor.StopAutonomousAction();
            }

            PredictedReconcileResult locomotionResult = _driver.Reconcile(
                appliedHint,
                in self,
                actor,
                ActionMotionReconcileGate.ResolveSnapThresholdMm(
                    actor,
                    in self,
                    authorityAction));
            if (locomotionResult.Snapped)
                actor.SnapPresentationToSimulation();
        }

        if (IsAuthorityHitOrDeath(in self) && actor != null)
        {
            ApplyAuthorityVitalityEdge(localPlayer, actor, in self);
            _driver?.SnapToSnapshot(in self);
            actor.SnapPresentationToSimulation();
        }
    }

    /// <summary>结束房间时清除 Owner 身份、预测历史与 HUD 状态。</summary>
    public void Reset()
    {
        _ownerActorId = default;
        _inputFrames = null;
        _driver = null;
        _actionAck = new PredictedActionAckQueue();
        _hasSelfSnapshot = false;
        _selfHealthMilli = -1;
    }

    /// <summary>首份 Owner 快照绑定 ActorId、对齐位姿并创建位移纠偏 Driver。</summary>
    void EnsurePredictedDriver(
        PlayerController localPlayer,
        in ActorReplicationSnapshot self)
    {
        if (_driver != null)
            return;
        if (localPlayer == null
            || localPlayer.CharacterConfig == null
            || localPlayer.Actor == null
            || _inputFrames == null)
        {
            return;
        }

        CharacterActor actor = localPlayer.Actor;
        CharacterConfig config = localPlayer.CharacterConfig;
        actor.BindSimulationInput(_ownerActorId, _inputFrames);
        actor.MotorSim.TeleportMm(self.PosXMm, self.PosYMm, self.PosZMm);
        actor.MotorSim.SetFacingMilliDeg(self.FacingMilliDeg);
        // MotorSim 与 Transform 必须同帧对齐，否则 +2m 客机出生偏移会进入第一次动作位移。
        actor.AlignSimulationRootToMotor();
        actor.SnapPresentationToSimulation();

        var predictConfig = new PredictedLocomotionConfig(
            MotionQuantization.MetersToMm(config.Motor.WalkSpeed),
            MotionQuantization.MetersToMm(config.Motor.RunSpeed),
            Mathf.RoundToInt(config.Motor.RunThreshold * 1000f),
            SimulationConfig.DefaultLogicHz,
            PredictedLocomotionConfig.Default.ReconcileThresholdMm,
            config.Motor.RotationSmoothTime,
            MotionQuantization.MetersToMm(config.Motor.SprintSpeed));
        _driver = new PredictedLocomotionDriver(actor.MotorSim, predictConfig);
    }

    /// <summary>取得本机当前预测招式的稳定 Catalog Id；空闲时返回 0。</summary>
    int ResolveLocalActionId(CharacterActor actor)
    {
        if (actor?.ActionSim == null || !actor.ActionSim.IsActive)
            return 0;
        if (actor.ActionSim.Snapshot.Content is ActionDefinition definition)
            return _content.Actions.GetOrAdd(definition);
        return 0;
    }

    /// <summary>把权威 Hit/Death 边沿写入本机状态机；不进入客户端命中 Pipeline。</summary>
    static void ApplyAuthorityVitalityEdge(
        PlayerController localPlayer,
        CharacterActor actor,
        in ActorReplicationSnapshot self)
    {
        CharacterConfig config = localPlayer != null ? localPlayer.CharacterConfig : null;
        var resolver = new CharacterReactionResolver(
            config != null ? config.Combat.Reactions : null);
        if (self.VitalityEdge == VitalityReplicationEdge.Death)
            actor.EnterDeath(resolver.ResolveDeath(default));
        else if (self.VitalityEdge == VitalityReplicationEdge.Hit)
            actor.EnterHit(resolver.ResolveHit(default));
    }

    /// <summary>受击或死亡必须覆盖预测位姿；普通动作只走 ACK/阈值和解。</summary>
    static bool IsAuthorityHitOrDeath(in ActorReplicationSnapshot snapshot) =>
        snapshot.VitalityEdge == VitalityReplicationEdge.Hit
        || snapshot.VitalityEdge == VitalityReplicationEdge.Death;
}

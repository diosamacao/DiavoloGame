using System;
using UnityEngine;

/// <summary>
/// 同机预测预览：走跑走 Autonomous Runner（禁止 Predict + 猜片）；出招立即播 Clip，权威硬直延迟到达后取消。
/// 不替换 Listen Host 本地玩家，不进花名册、不跑命中。
/// </summary>
[DefaultExecutionOrder(-39)]
[DisallowMultipleComponent]
public sealed class PredictedClientPreviewController : AppControllerBase
{
    [SerializeField] Vector3 worldOffset = new Vector3(-2f, 0f, 0f);
    [SerializeField] int latencyMs = 100;

    SimulationHost _host;
    CharacterConfig _config;
    LoopbackReplicationTransport _transport;
    ActionReplicationCatalog _catalog;
    PredictedLocomotionDriver _driver;
    AutonomousActionRunner _actionRunner;
    AutonomousLocomotionRunner _locomotionRunner;
    RemoteCharacterProxy _proxy;

    /// <summary>由战斗世界注入 Host、配置与预览参数。</summary>
    public void Configure(
        SimulationHost host,
        CharacterConfig config,
        Vector3 predictedWorldOffset,
        int remoteLatencyMs)
    {
        UnsubscribeHost();
        _host = host;
        _config = config;
        worldOffset = predictedWorldOffset;
        latencyMs = remoteLatencyMs < 0 ? 0 : remoteLatencyMs;
        if (isActiveAndEnabled)
            SubscribeHost();
    }

    void OnEnable() => SubscribeHost();

    void OnDisable() => UnsubscribeHost();

    void LateUpdate()
    {
        if (_proxy != null && _host != null)
            _proxy.Render(_host.InterpolationAlpha);
    }

    void OnDestroy()
    {
        UnsubscribeHost();
        _proxy?.Dispose();
        _proxy = null;
        _locomotionRunner = null;
        _driver = null;
        _actionRunner = null;
    }

    /// <summary>每逻辑步：走跑走 Runner；出招贴齐权威；延迟 Tick 只纠偏位姿。</summary>
    void OnAfterLogicStep(long authorityFrame)
    {
        ILocalPlayer local = SendQuery(new GetLocalPlayerQuery());
        CharacterActor actor = local?.Actor;
        if (actor == null || local.IsLocalPredicted)
            return;

        if (!TryEnsurePredicted(local, actor))
            return;

        ActorReplicationSnapshot authority = CharacterReplicationCapture.FromActor(actor, _catalog);
        InputFrame input = actor.LastSimulationInput;
        bool hostStaggered = IsAuthorityStaggered(actor);
        bool presentAction = hostStaggered
            || actor.CurrentState != CharacterStateType.Locomotion
            || authority.ActionId != 0;
        if (input.ActorId.IsValid)
        {
            if (hostStaggered)
            {
                // 权威已硬直：本地继续推预测招，等延迟 Tick 取消，禁止立刻贴受击位姿
                _locomotionRunner.Exit();
                _actionRunner.TickUnconfirmed(authorityFrame);
            }
            else
            {
                // 权威卡肉时不推本机 ActionSim，与房间客机同一条。
                _actionRunner.Tick(in input, authorityFrame, authority.FreezeFrames > 0);
                if (_actionRunner.IsActive || presentAction)
                {
                    _locomotionRunner.Exit();
                    _driver.PredictAligned(in input, actor.MotorSim);
                }
                else
                {
                    LocomotionResumeRequest resume = default;
                    if (!_locomotionRunner.IsActive)
                        resume = LocomotionResumeRequest.FromGait(actor.ReplicationGait);
                    _locomotionRunner.Tick(in input, in resume);
                    _driver.RecordAutonomous(in input);
                }
            }
        }

        _transport.SendAuthorityToClients(
            ReplicationCodec.WriteAuthorityTick(new AuthorityTick(authorityFrame, new[] { authority })));

        int stepMs = Mathf.Max(1, Mathf.RoundToInt(_host.FixedDeltaSeconds * 1000f));
        _transport.AdvanceTimeMs(stepMs);
        _transport.Pump();

        while (_transport.TryDequeueClient(out byte[] payload))
        {
            AuthorityTick received = ReplicationCodec.ReadAuthorityTick(payload);
            if (received.Actors.Length == 0)
                continue;
            // 走跑带 Runner：默认 2m 硬吸，与房间客机相同。
            PredictedReconcileResult loco = _driver.Reconcile(
                received.AuthorityFrame,
                in received.Actors[0],
                _locomotionRunner);
            _actionRunner.Reconcile(received.AuthorityFrame, in received.Actors[0]);
            // 与房间客机相同：吸附后对齐表现锚点，禁止插值扫回拉。
            if (loco.Snapped)
                _proxy.SnapPresentationToSimulation();
        }

        // 出招贴齐可能被延迟 Tick 纠偏冲掉；走跑由 Runner 写电机，不再 Snap
        // 出招只对齐电机，禁止每帧掐插值；受击才硬切表现。
        if (presentAction && !hostStaggered)
            _driver.SnapMotorTo(actor.MotorSim);
        if (hostStaggered)
            _proxy.SnapPresentationToSimulation();

        // 与房间客机相同：本机招打完后不要用延迟/仍在播的权威招再派一遍 VFX。
        if (!_actionRunner.IsActive && authority.ActionId == 0 && !hostStaggered)
            _actionRunner.NotifyAuthorityIdle();

        bool followAuthorityAction = hostStaggered
            || PredictedActionAckQueue.ShouldPresentAuthorityAction(
                _actionRunner.IsActive,
                _actionRunner.SuppressStaleAuthorityAction,
                hostStaggered,
                authority.ActionId);
        if (_actionRunner.IsActive || followAuthorityAction)
        {
            ActorReplicationSnapshot visual = authority
                .WithMotorPose(_driver.Motor)
                .WithAction(
                    _actionRunner.IsActive ? _actionRunner.ActionId : authority.ActionId,
                    _actionRunner.IsActive ? _actionRunner.ActionFrame : authority.ActionFrame,
                    authority.FreezeFrames);
            _proxy.ApplySnapshot(in visual, hostStaggered ? 0f : actor.SprintLeanRollDegrees);
        }
        else
        {
            _proxy.SyncAutonomousLocomotion(
                _locomotionRunner.LeanRollDegrees,
                _locomotionRunner.DebugWishWorld);
        }
    }

    /// <summary>首次创建预测电机、Loopback 与表现体；位姿与权威对齐后才开始预测。</summary>
    bool TryEnsurePredicted(ILocalPlayer local, CharacterActor actor)
    {
        if (_proxy != null && _driver != null && _locomotionRunner != null && _actionRunner != null)
            return true;

        CharacterConfig config = _config;
        if (config == null && local is PlayerController player)
            config = player.CharacterConfig;
        if (config == null || _host == null)
            return false;

        _config = config;
        _catalog = new ActionReplicationCatalog();
        _transport = new LoopbackReplicationTransport();
        _transport.SetLatencyMs(latencyMs);

        AutonomousPredictedSeat seat = RemoteCharacterProxyFactory.CreateAutonomous(
            config,
            _catalog,
            _host.CollisionWorld,
            worldOffset,
            _host.FixedDeltaSeconds,
            transform);
        _proxy = seat.Proxy;
        _locomotionRunner = seat.Runner;
        _actionRunner = seat.Action;
        _proxy.MotorSim.TeleportMm(
            actor.MotorSim.PositionMm.X,
            actor.MotorSim.YMm,
            actor.MotorSim.PositionMm.Z);
        _proxy.MotorSim.SetFacingMilliDeg(actor.MotorSim.FacingMilliDeg);

        var predictConfig = new PredictedLocomotionConfig(
            MotionQuantization.MetersToMm(config.Motor.WalkSpeed),
            MotionQuantization.MetersToMm(config.Motor.RunSpeed),
            Mathf.RoundToInt(config.Motor.RunThreshold * 1000f),
            SimulationConfig.DefaultLogicHz,
            PredictedLocomotionConfig.Default.ReconcileThresholdMm,
            config.Motor.RotationSmoothTime,
            MotionQuantization.MetersToMm(config.Motor.SprintSpeed));
        _driver = new PredictedLocomotionDriver(_proxy.MotorSim, predictConfig);
        return true;
    }

    /// <summary>权威已进受击/死亡：预测侧不得立刻跟，否则看不到延迟取消。</summary>
    static bool IsAuthorityStaggered(CharacterActor actor) =>
        actor.CurrentState == CharacterStateType.Hit
        || actor.CurrentState == CharacterStateType.Death;

    void SubscribeHost()
    {
        if (_host != null)
            _host.AfterLogicStep += OnAfterLogicStep;
    }

    void UnsubscribeHost()
    {
        if (_host != null)
            _host.AfterLogicStep -= OnAfterLogicStep;
    }
}

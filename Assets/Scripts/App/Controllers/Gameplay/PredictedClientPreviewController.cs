using System;
using UnityEngine;

/// <summary>
/// NS3/NS4 同机预测预览：稳态走跑 FollowInput；出招立即播 Clip，权威硬直延迟到达后取消。
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
    PredictedActionDriver _actionDriver;
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
    }

    /// <summary>每逻辑步：稳态 FollowInput 预测或贴齐权威，再对延迟 Tick 和解并刷新 lean/残差。</summary>
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
        bool alignToAuthority = !hostStaggered && ShouldAlignToAuthority(actor, in authority);
        if (input.ActorId.IsValid)
        {
            if (hostStaggered)
            {
                // 权威已硬直：本地继续播预测招，等延迟 Tick 取消，禁止立刻贴受击位姿
                _actionDriver.TickUnconfirmed(authorityFrame);
            }
            else if (alignToAuthority)
            {
                _driver.PredictAligned(in input, actor.MotorSim);
                _actionDriver.Predict(authorityFrame, authority.ActionId, authority.ActionFrame);
            }
            else
            {
                _driver.Predict(in input, ResolvePredictedPlanarSpeedMm(actor, _driver.Config));
                _actionDriver.Predict(authorityFrame, authority.ActionId, authority.ActionFrame);
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
            _driver.Reconcile(received.AuthorityFrame, in received.Actors[0]);
            _actionDriver.Reconcile(received.AuthorityFrame, in received.Actors[0]);
        }

        // 纠偏重放可能冲掉本帧贴齐；出招/转身仍以权威电机为准
        if (alignToAuthority)
            _driver.SnapMotorTo(actor.MotorSim);

        ActorReplicationSnapshot visual = authority
            .WithMotorPose(_driver.Motor)
            .WithAction(_actionDriver.ActionId, _actionDriver.ActionFrame, authority.FreezeFrames);
        _proxy.ApplySnapshot(in visual, hostStaggered ? 0f : actor.SprintLeanRollDegrees);
    }

    /// <summary>首次创建预测电机、Loopback 与表现体；位姿与权威对齐后才开始预测。</summary>
    bool TryEnsurePredicted(ILocalPlayer local, CharacterActor actor)
    {
        if (_proxy != null && _driver != null)
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

        var predictMotor = new CharacterMotorSim(
            _host.CollisionWorld,
            MotionQuantization.MetersToMm(config.Motor.ControllerRadius),
            config.Motor.SoftBodyMass,
            config.Motor.SoftBodyImmovable,
            SimulationConfig.DefaultLogicHz,
            MotionQuantization.MetersToMm(config.Motor.Gravity),
            MotionQuantization.MetersToMm(config.Motor.GroundedGravity));
        predictMotor.TeleportMm(
            actor.MotorSim.PositionMm.X,
            actor.MotorSim.YMm,
            actor.MotorSim.PositionMm.Z);
        predictMotor.SetFacingMilliDeg(actor.MotorSim.FacingMilliDeg);

        var predictConfig = new PredictedLocomotionConfig(
            MotionQuantization.MetersToMm(config.Motor.WalkSpeed),
            MotionQuantization.MetersToMm(config.Motor.RunSpeed),
            Mathf.RoundToInt(config.Motor.RunThreshold * 1000f),
            SimulationConfig.DefaultLogicHz,
            PredictedLocomotionConfig.Default.ReconcileThresholdMm,
            config.Motor.RotationSmoothTime,
            MotionQuantization.MetersToMm(config.Motor.SprintSpeed));
        _driver = new PredictedLocomotionDriver(predictMotor, predictConfig);
        _actionDriver = new PredictedActionDriver();
        _proxy = RemoteCharacterProxyFactory.Create(
            config,
            _catalog,
            _host.CollisionWorld,
            worldOffset,
            _host.FixedDeltaSeconds,
            transform);
        return true;
    }

    /// <summary>权威已进受击/死亡：预测侧不得立刻跟，否则看不到延迟取消。</summary>
    static bool IsAuthorityStaggered(CharacterActor actor) =>
        actor.CurrentState == CharacterStateType.Hit
        || actor.CurrentState == CharacterStateType.Death;

    /// <summary>
    /// 出招与起步/折返/急停等烘焙相位：跟权威位姿。
    /// 受击/死亡不贴齐；稳态走跑冲刺走 FollowInput 预测。
    /// </summary>
    static bool ShouldAlignToAuthority(CharacterActor actor, in ActorReplicationSnapshot snapshot)
    {
        if (IsAuthorityStaggered(actor))
            return false;
        if (actor.CurrentState != CharacterStateType.Locomotion)
            return true;
        return ReplicationPresentationAlign.ShouldAlignFromSnapshot(in snapshot);
    }

    /// <summary>按权威当前 AnimationKey 选走/跑/冲刺速度；未知则让数学层按输入幅度估。</summary>
    static int ResolvePredictedPlanarSpeedMm(CharacterActor actor, in PredictedLocomotionConfig config)
    {
        if (actor.Animation == null || !actor.Animation.CurrentKey.HasValue)
            return 0;

        switch (actor.Animation.CurrentKey.Value)
        {
            case AnimationKey.Sprint:
                return config.SprintSpeedMm;
            case AnimationKey.Walk:
            case AnimationKey.WalkLeft:
            case AnimationKey.WalkRight:
                return config.WalkSpeedMm;
            case AnimationKey.Run:
                return config.RunSpeedMm;
            default:
                return 0;
        }
    }

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

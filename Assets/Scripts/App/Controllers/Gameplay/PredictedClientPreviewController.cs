using System;
using UnityEngine;

/// <summary>
/// 同机预测预览：左侧跑 Autonomous CharacterActor，权威硬直延迟到达后取消。
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
    readonly PredictedActionAckQueue _actionAck = new();
    CharacterActor _preview;
    GameObject _previewOwner;
    int _lastPredictedActionId;

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
        if (_preview != null && _host != null)
            _preview.Render(_host.InterpolationAlpha);
    }

    void OnDestroy()
    {
        UnsubscribeHost();
        DisposePreview();
    }

    /// <summary>每逻辑步：预览 Actor.Step；延迟 Tick 只纠偏位姿。</summary>
    void OnAfterLogicStep(long authorityFrame)
    {
        ILocalPlayer local = SendQuery(new GetLocalPlayerQuery());
        CharacterActor hostActor = local?.Actor;
        if (hostActor == null || local.IsLocalPredicted)
            return;

        if (!TryEnsurePredicted(local, hostActor))
            return;

        ActorReplicationSnapshot authority = CharacterReplicationCapture.FromActor(hostActor, _catalog);
        InputFrame input = hostActor.LastSimulationInput;
        bool hostStaggered = IsAuthorityStaggered(hostActor);
        if (input.ActorId.IsValid)
        {
            _preview.SetAutonomousPredictMode(suppressNewStarts: hostStaggered);
            _preview.Step(authorityFrame, _host.FixedDeltaSeconds, in input);
            _preview.ResolvePostCombat(authorityFrame);
            int actionId = ResolvePreviewActionId();
            if (actionId != 0)
                _lastPredictedActionId = actionId;
            _actionAck.Record(authorityFrame, actionId);
            _driver.RecordAutonomous(in input);
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

            PredictedActionReconcileResult actionResult =
                _actionAck.Reconcile(received.AuthorityFrame, in received.Actors[0]);
            ActorReplicationSnapshot delayed = received.Actors[0];
            // 与真客机相同：权威仍在出招时不回 Idle；本机已收招则跟延迟 Tick 的招
            if (PredictedActionAckQueue.ShouldStopAutonomousAction(actionResult, in delayed))
                _preview.StopAutonomousAction();

            _catalog.TryGet(received.Actors[0].ActionId, out ActionDefinition authorityAction);
            PredictedReconcileResult loco = _driver.Reconcile(
                received.AuthorityFrame,
                in received.Actors[0],
                _preview,
                ActionMotionReconcileGate.ResolveSnapThresholdMm(
                    _preview,
                    in received.Actors[0],
                    authorityAction));
            if (loco.Snapped)
                _preview.SnapPresentationToSimulation();
        }

        if (hostStaggered)
            _preview.SnapPresentationToSimulation();
    }

    /// <summary>首次创建 Autonomous 预览 Actor 与 Loopback；位姿与权威对齐后才开始预测。</summary>
    bool TryEnsurePredicted(ILocalPlayer local, CharacterActor hostActor)
    {
        if (_preview != null && _driver != null)
            return true;

        CharacterConfig config = _config;
        if (config == null && local is PlayerController player)
            config = player.CharacterConfig;
        if (config == null || _host == null)
            return false;

        _config = config;
        _catalog = new ActionReplicationCatalog();
        _catalog.Prefill(config);
        _transport = new LoopbackReplicationTransport();
        _transport.SetLatencyMs(latencyMs);

        _previewOwner = new GameObject("PredictedClientPreviewActor");
        _previewOwner.transform.SetParent(transform, false);
        Vector3 spawn = hostActor.PresentationRoot != null
            ? hostActor.PresentationRoot.position + worldOffset
            : worldOffset;
        _previewOwner.transform.position = spawn;

        _lastPredictedActionId = 0;
        _preview = CharacterActorFactory.Create(
            _previewOwner,
            _previewOwner.transform,
            config,
            config.Combat.TeamId,
            null,
            () => Array.Empty<IHurtboxTarget>(),
            null,
            out ActionSim _,
            out CharacterAnimationService _,
            _host.CollisionWorld,
            null,
            null,
            ReplicationSeat.Autonomous);
        _preview.MotorSim.TeleportMm(
            hostActor.MotorSim.PositionMm.X,
            hostActor.MotorSim.YMm,
            hostActor.MotorSim.PositionMm.Z);
        _preview.MotorSim.SetFacingMilliDeg(hostActor.MotorSim.FacingMilliDeg);
        _preview.AlignSimulationRootToMotor();
        _preview.SnapPresentationToSimulation();

        var predictConfig = new PredictedLocomotionConfig(
            MotionQuantization.MetersToMm(config.Motor.WalkSpeed),
            MotionQuantization.MetersToMm(config.Motor.RunSpeed),
            Mathf.RoundToInt(config.Motor.RunThreshold * 1000f),
            SimulationConfig.DefaultLogicHz,
            PredictedLocomotionConfig.Default.ReconcileThresholdMm,
            config.Motor.RotationSmoothTime,
            MotionQuantization.MetersToMm(config.Motor.SprintSpeed));
        _driver = new PredictedLocomotionDriver(_preview.MotorSim, predictConfig);
        return true;
    }

    /// <summary>预览体当前招 Catalog Id。</summary>
    int ResolvePreviewActionId()
    {
        if (_preview?.ActionSim == null || !_preview.ActionSim.IsActive)
            return 0;
        if (_preview.ActionSim.Snapshot.Content is ActionDefinition definition)
            return _catalog.GetOrAdd(definition);
        return 0;
    }

    /// <summary>权威已进受击/死亡：预测侧不得立刻跟，否则看不到延迟取消。</summary>
    static bool IsAuthorityStaggered(CharacterActor actor) =>
        actor.CurrentState == CharacterStateType.Hit
        || actor.CurrentState == CharacterStateType.Death;

    void DisposePreview()
    {
        _preview?.Dispose();
        _preview = null;
        _driver = null;
        if (_previewOwner != null)
        {
            Destroy(_previewOwner);
            _previewOwner = null;
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

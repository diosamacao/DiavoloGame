using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 客机本机只读出招：同一套 <see cref="ActionSim"/> 解析、推帧、Cancel 窗。
/// 不 Collect、不写 Numeric、不跑 <see cref="ActionMotionResolver"/>。
/// 自然结束后由房间忽略延迟权威招，避免 Clip/VFX 重播。
/// </summary>
public sealed class AutonomousActionRunner
{
    readonly ActionSim _sim;
    readonly GameplayIntentBuffer _intentBuffer;
    readonly GameplayIntentProducer _intentProducer;
    readonly ActionResolverService _resolverService;
    readonly InputManager _input;
    readonly Transform _actorRoot;
    readonly CharacterMotor _motor;
    readonly ActionReplicationCatalog _catalog;
    readonly PredictedActionAckQueue _ack = new();
    readonly List<ActionSimEvent> _discardEvents = new(16);
    readonly ActionPresentationProbe _probe;

    int _lastActionId;
    /// <summary>本机这一轮已经起过预测招；自然结束后用来挡住延迟权威招。</summary>
    bool _predictedSession;
    /// <summary>和解真取消或受击后改跟快照，直到权威空闲。</summary>
    bool _followAuthorityAction;

    /// <summary>
    /// 在预测体 Motor/Animation 上装配只读 ActionSim；resourceGate 必须为空。
    /// </summary>
    public AutonomousActionRunner(
        CharacterConfig config,
        ActionReplicationCatalog catalog,
        Transform actorRoot,
        CharacterMotor motor,
        CharacterAnimationService animation,
        InputManager input,
        LocomotionStateMachine locomotion)
    {
        if (config == null)
            throw new System.ArgumentNullException(nameof(config));
        _catalog = catalog ?? throw new System.ArgumentNullException(nameof(catalog));
        _actorRoot = actorRoot != null ? actorRoot : throw new System.ArgumentNullException(nameof(actorRoot));
        _motor = motor ?? throw new System.ArgumentNullException(nameof(motor));
        _input = input ?? throw new System.ArgumentNullException(nameof(input));
        if (animation == null)
            throw new System.ArgumentNullException(nameof(animation));

        GameplayIntentProfile intentProfile = GameplayIntentSettings.Active;
        if (intentProfile == null)
            throw new System.InvalidOperationException("AutonomousActionRunner: 全局 GameplayIntentProfile 未就绪。");
        if (config.CombatProfile == null)
            throw new System.InvalidOperationException("AutonomousActionRunner: CharacterConfig 未绑定 CombatModeProfile。");

        var combatMode = new CombatModeService(config.CombatProfile, animation);
        _resolverService = new ActionResolverService(combatMode);
        _intentBuffer = new GameplayIntentBuffer(intentProfile.ActionBufferDurationFrames);
        var resolverBridge = new ActionSimResolverBridge(
            _resolverService,
            actorRoot,
            motor,
            resourceGate: null);
        _sim = new ActionSim(resolverBridge, _intentBuffer, resourceGate: null);
        _probe = new ActionPresentationProbe(this);
        _intentProducer = new GameplayIntentProducer(
            intentProfile,
            input,
            _intentBuffer,
            _probe,
            locomotion,
            _sim);
    }

    /// <summary>只读 ActionSim 是否仍有活动招。</summary>
    public bool IsActive => _sim.IsActive;

    /// <summary>当前预测招 Catalog Id；无招为 0。</summary>
    public int ActionId
    {
        get
        {
            ActionDefinition action = CurrentAction;
            return action != null ? _catalog.GetOrAdd(action) : 0;
        }
    }

    /// <summary>当前预测招逻辑帧。</summary>
    public int ActionFrame => _sim.CurrentFrame;

    /// <summary>最近一次非 0 预测招 Id，供闪避后 Sprint 恢复。</summary>
    public int LastActionId => _lastActionId;

    /// <summary>
    /// 本机已自然结束预测招、且未因和解改跟权威时为 true。
    /// 房间必须忽略延迟快照上的 ActionId，否则会重播 Clip/VFX。
    /// </summary>
    public bool SuppressStaleAuthorityAction =>
        _predictedSession && !IsActive && !_followAuthorityAction;

    /// <summary>当前招式定义；无招为空。</summary>
    public ActionDefinition CurrentAction => _sim.Snapshot.Content as ActionDefinition;

    /// <summary>
    /// 摄入本帧输入：生产意图、起手/Cancel、推 ActionSim 一帧。禁止 Collect。
    /// authorityFrozen：权威仍在卡肉时不推帧、不起手，避免 Clip 暂停时 ActionFrame 跑飞。
    /// </summary>
    public void Tick(in InputFrame input, long frame, bool authorityFrozen = false)
    {
        _input.IngestFrame(input);
        _intentProducer.Step();
        if (authorityFrozen)
        {
            BufferFrameIntentsOnly();
            DrainDiscard();
        }
        else
        {
            RouteIntents();
            if (_sim.IsActive)
            {
                _sim.Step();
                _sim.ResolvePostCombat();
                DrainDiscard();
            }
        }

        RememberAction();
        _ack.Record(frame, ActionId);
    }

    /// <summary>权威已硬直：不新起手，只推进已预测招，等延迟 Tick 取消。</summary>
    public void TickUnconfirmed(long frame)
    {
        if (_sim.IsActive)
        {
            _sim.Step();
            _sim.ResolvePostCombat();
            DrainDiscard();
        }

        RememberAction();
        _ack.Record(frame, ActionId);
    }

    /// <summary>
    /// 按权威帧和解。Cancelled 时 Stop 本地招。
    /// 权威仍有招或受击：表现改跟快照；权威未起手：回走跑，忽略延迟招。
    /// 同招不 Seek 回旧帧。连招超前不 Cancel。
    /// </summary>
    public PredictedActionReconcileResult Reconcile(
        long authorityFrame,
        in ActorReplicationSnapshot authority)
    {
        PredictedActionReconcileResult result = _ack.Reconcile(authorityFrame, in authority);
        if (result.Cancelled)
        {
            bool followAuthority = authority.ActionId != 0
                || authority.VitalityEdge == VitalityReplicationEdge.Hit
                || authority.VitalityEdge == VitalityReplicationEdge.Death;
            Stop(followAuthority);
        }

        return result;
    }

    /// <summary>
    /// 立即停本地招。followAuthority：受击或变体分叉后跟快照；自然结束/未起手不要传 true。
    /// </summary>
    public void Stop(bool followAuthority = false)
    {
        _sim.Stop();
        DrainDiscard();
        _intentBuffer.ClearAllBuffers();
        if (followAuthority)
            _followAuthorityAction = true;
    }

    /// <summary>权威已空闲：结束本机出招会话，下次未预测的权威招可以跟快照。</summary>
    public void NotifyAuthorityIdle()
    {
        if (IsActive)
            return;

        _predictedSession = false;
        _followAuthorityAction = false;
    }

    void RouteIntents()
    {
        if (!_sim.IsActive)
        {
            for (int i = 0; i < _intentBuffer.FrameIntents.Count; i++)
                TryStartFromLocomotion(_intentBuffer.FrameIntents[i]);
            return;
        }

        if (_input.HasMoveIntent && _sim.CanCancelByMovement)
        {
            Stop();
            return;
        }

        for (int i = 0; i < _intentBuffer.FrameIntents.Count; i++)
        {
            GameplayIntentType intent = _intentBuffer.FrameIntents[i];
            if (!TryPriorityInterrupt(intent))
                _intentBuffer.Buffer(intent);
        }
    }

    void TryStartFromLocomotion(GameplayIntentType intent)
    {
        if (intent == GameplayIntentType.None || _sim.IsActive)
            return;

        var request = new ActionRequest(intent);
        var context = new ActionResolveContext(
            ActionResolveOrigin.LocomotionStart,
            null,
            _actorRoot,
            _motor,
            canAfford: _ => true);
        if (!_resolverService.TryResolveStart(in request, in context, out ActionResolveResult resolveResult))
            return;

        ActionSimResolveResult simResult = resolveResult.ToSimResult();
        if (!_sim.TryStart(in simResult))
        {
            _intentBuffer.Buffer(intent);
            return;
        }

        _intentBuffer.ClearBuffer(intent);
        ClearOtherBufferedIntents(intent);
    }

    bool TryPriorityInterrupt(GameplayIntentType intent)
    {
        ActionDefinition current = CurrentAction;
        if (current == null)
            return false;

        var request = new ActionRequest(intent);
        var context = new ActionResolveContext(
            ActionResolveOrigin.PriorityInterrupt,
            current,
            _actorRoot,
            _motor,
            canAfford: _ => true);
        if (!_resolverService.TryResolveStart(in request, in context, out ActionResolveResult resolveResult))
            return false;

        ActionSimResolveResult simResult = resolveResult.ToSimResult();
        if (!_sim.TryInterrupt(in simResult))
            return false;

        _intentBuffer.ClearBuffer(intent);
        return true;
    }

    void ClearOtherBufferedIntents(GameplayIntentType keepIntent)
    {
        foreach (GameplayIntentType intent in _resolverService.EnumerateActiveIntents())
        {
            if (intent != keepIntent)
                _intentBuffer.ClearBuffer(intent);
        }
    }

    /// <summary>卡肉期间只把当帧意图写入 Cancel 缓冲，禁止起手/移动取消。</summary>
    void BufferFrameIntentsOnly()
    {
        for (int i = 0; i < _intentBuffer.FrameIntents.Count; i++)
            _intentBuffer.Buffer(_intentBuffer.FrameIntents[i]);
    }

    void RememberAction()
    {
        int id = ActionId;
        if (id == 0)
            return;

        _lastActionId = id;
        _predictedSession = true;
        _followAuthorityAction = false;
    }

    /// <summary>丢掉 Started/Frame 事件，禁止外层拿去派发 Hitbox。</summary>
    void DrainDiscard()
    {
        _discardEvents.Clear();
        _sim.DrainEvents(_discardEvents);
        _discardEvents.Clear();
    }

    /// <summary>只给意图生产器读「是否在播招」；禁止改顶层状态机。</summary>
    sealed class ActionPresentationProbe : ICharacterStateMachine
    {
        readonly AutonomousActionRunner _owner;

        public ActionPresentationProbe(AutonomousActionRunner owner) => _owner = owner;

        public CharacterStateType CurrentStateId =>
            _owner.IsActive ? CharacterStateType.Action : CharacterStateType.Locomotion;

        public bool TryChangeState(CharacterStateType next, bool force = false) => false;
    }
}

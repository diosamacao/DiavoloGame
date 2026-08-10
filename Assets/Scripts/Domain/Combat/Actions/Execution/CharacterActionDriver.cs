using UnityEngine;

/// <summary>角色招式输入路由：起手、高优硬打断、缓冲、移动取消与离开 Action 后的预输入消费；玩家与敌人共用。</summary>
public sealed class CharacterActionDriver
{
    readonly CombatTargetLock targetLock;
    readonly IMoveIntentSource _moveIntent;
    readonly GameplayIntentBuffer _intentBuffer;
    readonly CharacterStateMachine _stateMachine;
    readonly ActionSim _actionSim;
    /// <summary>同物体 CombatModeService；用具体类型访问 Profile / TrySetMode 三参 overload。</summary>
    readonly CombatModeService _combatMode;
    /// <summary>Locomotion 起手 / Cancel 解析（委托 ActionGraph）。</summary>
    readonly ActionResolverService _resolverService;
    /// <summary>角色根节点；方向 Resolver 读取朝向用。</summary>
    readonly Transform _actorRoot;
    /// <summary>招式解析上下文（闪避意图、朝向修正）。</summary>
    readonly IActionStartContext _startContext;
    /// <summary>可选离散 Entry 请求源；玩家通常为空，AI/脚本控制可注入。</summary>
    readonly IActionEntryRequestSource _entryRequests;
    bool _wasInAction;

    /// <summary>创建纯 C# 招式意图路由；物理输入映射由 GameplayIntentProducer 持有。</summary>
    public CharacterActionDriver(
        IMoveIntentSource moveIntent,
        GameplayIntentBuffer intentBuffer,
        CharacterStateMachine stateMachine,
        ActionSim actionSim,
        CombatModeService combatMode,
        CombatTargetLock lockState,
        ActionResolverService resolverService,
        Transform actorRoot,
        IActionStartContext startContext,
        IActionEntryRequestSource entryRequests = null)
    {
        _moveIntent = moveIntent;
        _intentBuffer = intentBuffer;
        _stateMachine = stateMachine;
        _actionSim = actionSim;
        _combatMode = combatMode;
        targetLock = lockState;
        _resolverService = resolverService;
        _actorRoot = actorRoot;
        _startContext = startContext;
        _entryRequests = entryRequests;
    }

    /// <summary>每帧在 GameplayIntentProducer 之后调用，负责起手、动作缓冲和移动取消。</summary>
    public void ProcessGameplayInput()
    {
        bool inAction = _stateMachine.CurrentStateId == CharacterStateType.Action;
        if (_wasInAction && !inAction)
        {
            targetLock.ClearLock();
            _combatMode.ApplyPendingModeIfReady();
            // 费用不足时保留缓冲至自然过期，禁止离开 Action 时一刀清空
            TryStartFromBufferedInputs();
        }

        if (inAction)
            TryCancelActionByMovement();

        // 外部离散 Entry 优先于同帧 Intent；空 InputFrame 的 AI 不产生玩家意图
        TryConsumeEntryRequest();

        // 语义意图在 Producer 内已完成物理输入与上下文判定；Driver 只按当前顶层状态路由。
        if (_intentBuffer != null)
        {
            for (int i = 0; i < _intentBuffer.FrameIntents.Count; i++)
                HandleGameplayIntent(_intentBuffer.FrameIntents[i]);
        }

        _wasInAction = _stateMachine.CurrentStateId == CharacterStateType.Action;
    }

    /// <summary>消费离散 Entry 请求；非 Locomotion / 解析失败 / 费用失败均不卡死。</summary>
    void TryConsumeEntryRequest()
    {
        if (_entryRequests == null || !_entryRequests.TryConsume(out ActionEntryRequest request))
            return;

        if (_stateMachine.CurrentStateId != CharacterStateType.Locomotion)
            return;

        TryStartRequestedEntry(request.EntryNodeId);
    }

    /// <summary>按 Graph Entry NodeId 起手（CostGate 同玩家 Intent 路径）。</summary>
    public bool TryStartRequestedEntry(string entryNodeId)
    {
        if (_resolverService == null || _actionSim == null || string.IsNullOrEmpty(entryNodeId))
            return false;
        if (_stateMachine.CurrentStateId != CharacterStateType.Locomotion)
            return false;

        var context = new ActionResolveContext(
            ActionResolveOrigin.LocomotionStart,
            null,
            _actorRoot,
            _startContext,
            canAfford: content => _actionSim.CanAffordContent(content));

        if (!_resolverService.TryResolveEntry(entryNodeId, in context, out ActionResolveResult resolveResult))
            return false;

        ActionSimResolveResult simResult = resolveResult.ToSimResult();
        if (!_actionSim.TryStart(in simResult))
            return false;

        _stateMachine.TryChangeState(CharacterStateType.Action);
        return true;
    }

    /// <summary>进入受控或死亡状态时清空动作缓冲与索敌锁定。</summary>
    public void ClearPendingActions()
    {
        ClearAllActionBuffers();
        targetLock.ClearLock();
        _wasInAction = false;
    }

    /// <summary>离开 Action 后尝试用缓冲的离散输入从 Locomotion 起手。</summary>
    bool TryStartFromBufferedInputs()
    {
        if (_resolverService == null || _intentBuffer == null)
            return false;

        foreach (GameplayIntentType intent in _resolverService.EnumerateActiveIntents())
        {
            if (!_intentBuffer.HasBuffer(intent))
                continue;

            TryStartFromLocomotion(intent);
            return _stateMachine.CurrentStateId == CharacterStateType.Action;
        }

        return false;
    }

    void ClearAllActionBuffers()
    {
        _intentBuffer?.ClearAllBuffers();
    }

    /// <summary>移动取消：在 CancelWindow(Movement) 内退回 Locomotion。</summary>
    void TryCancelActionByMovement()
    {
        if (!_moveIntent.HasMoveIntent)
            return;

        if (_actionSim == null || !_actionSim.CanCancelByMovement)
            return;

        _stateMachine.TryChangeState(CharacterStateType.Locomotion);
    }

    void HandleGameplayIntent(GameplayIntentType intent)
    {
        if (intent == GameplayIntentType.None)
            return;

        if (_stateMachine.CurrentStateId == CharacterStateType.Locomotion)
            TryStartFromLocomotion(intent);
        else if (_stateMachine.CurrentStateId == CharacterStateType.Action)
        {
            // 先尝试高优 Entry 硬打断；失败则缓冲留给 CancelWindow 连招。
            if (!TryPriorityInterrupt(intent))
                _intentBuffer.Buffer(intent);
        }
    }

    /// <summary>
    /// Action 态高优硬打断：按 Graph Entry 解析候选招，成功则消费意图缓冲。
    /// </summary>
    bool TryPriorityInterrupt(GameplayIntentType intent)
    {
        if (_resolverService == null || _actionSim == null)
            return false;

        ActionDefinition current = _actionSim.Snapshot.Content as ActionDefinition;
        if (current == null)
            return false;

        var request = new ActionRequest(intent);
        var context = new ActionResolveContext(
            ActionResolveOrigin.PriorityInterrupt,
            current,
            _actorRoot,
            _startContext,
            canAfford: content => _actionSim.CanAffordContent(content));

        if (!_resolverService.TryResolveStart(in request, in context, out ActionResolveResult resolveResult))
            return false;

        ActionSimResolveResult simResult = resolveResult.ToSimResult();
        if (!_actionSim.TryInterrupt(in simResult))
            return false;

        _intentBuffer?.ClearBuffer(intent);
        return true;
    }

    /// <summary>Locomotion 起手：经 ActionGraph Entry×Intent 解析后交给 ActionSim。</summary>
    void TryStartFromLocomotion(GameplayIntentType intent)
    {
        var request = new ActionRequest(intent);
        var context = new ActionResolveContext(
            ActionResolveOrigin.LocomotionStart,
            null,
            _actorRoot,
            _startContext,
            canAfford: content => _actionSim.CanAffordContent(content));

        if (!_resolverService.TryResolveStart(in request, in context, out ActionResolveResult resolveResult))
            return;

        ActionSimResolveResult simResult = resolveResult.ToSimResult();
        // 费用不足时 TryStart 失败：写入/保留缓冲，便于攒够能量后仍能放出
        if (!_actionSim.TryStart(in simResult))
        {
            _intentBuffer?.Buffer(intent);
            return;
        }

        _intentBuffer.ClearBuffer(intent);
        // 清掉残留意图（尤其 AttackRelease），避免进蓄力首帧 Cancel 窗立刻秒放 1 档。
        ClearOtherBufferedIntents(intent);
        _stateMachine.TryChangeState(CharacterStateType.Action);
    }

    /// <summary>起手成功后清除其它动作缓冲；不保留任何旁路意图。</summary>
    void ClearOtherBufferedIntents(GameplayIntentType keepIntent)
    {
        if (_intentBuffer == null || _resolverService == null)
            return;

        foreach (GameplayIntentType intent in _resolverService.EnumerateActiveIntents())
        {
            if (intent == keepIntent)
                continue;

            _intentBuffer.ClearBuffer(intent);
        }
    }
}

using UnityEngine;

/// <summary>角色招式输入路由：离散输入起手/缓冲、移动取消与离开 Action 后的预输入消费；玩家与敌人共用。</summary>
public sealed class CharacterActionDriver
{
    readonly CombatTargetLock targetLock;
    readonly InputManager _input;
    readonly GameplayIntentBuffer _intentBuffer;
    readonly CharacterStateMachine _stateMachine;
    readonly ActionExecutor _actionExecutor;
    /// <summary>同物体 CombatModeService；用具体类型访问 Profile / TrySetMode 三参 overload。</summary>
    readonly CombatModeService _combatMode;
    /// <summary>Locomotion 起手 / Cancel 解析（委托 ActionGraph）。</summary>
    readonly ActionResolverService _resolverService;
    /// <summary>角色根节点；方向 Resolver 读取朝向用。</summary>
    readonly Transform _actorRoot;
    /// <summary>招式起手副作用上下文（闪避意图、朝向修正）；与 ActionExecutor 共用同一实例。</summary>
    readonly IActionStartContext _startContext;
    bool _wasInAction;

    /// <summary>创建纯 C# 招式意图路由；物理输入映射由 GameplayIntentProducer 持有。</summary>
    public CharacterActionDriver(
        InputManager input,
        GameplayIntentBuffer intentBuffer,
        CharacterStateMachine stateMachine,
        ActionExecutor actionExecutor,
        CombatModeService combatMode,
        CombatTargetLock lockState,
        ActionResolverService resolverService,
        Transform actorRoot,
        IActionStartContext startContext)
    {
        _input = input;
        _intentBuffer = intentBuffer;
        _stateMachine = stateMachine;
        _actionExecutor = actionExecutor;
        _combatMode = combatMode;
        targetLock = lockState;
        _resolverService = resolverService;
        _actorRoot = actorRoot;
        _startContext = startContext;
    }

    /// <summary>每帧在 GameplayIntentProducer 之后调用，负责起手、动作缓冲和移动取消。</summary>
    public void ProcessGameplayInput()
    {
        bool inAction = _stateMachine.CurrentStateId == CharacterStateType.Action;
        if (_wasInAction && !inAction)
        {
            targetLock.ClearLock();
            _combatMode.ApplyPendingModeIfReady();

            if (!TryStartFromBufferedInputs())
                ClearAllActionBuffers();
        }

        if (inAction)
            TryCancelActionByMovement();

        // 语义意图在 Producer 内已完成物理输入与上下文判定；Driver 只按当前顶层状态路由。
        if (_intentBuffer != null)
        {
            for (int i = 0; i < _intentBuffer.FrameIntents.Count; i++)
                HandleGameplayIntent(_intentBuffer.FrameIntents[i]);
        }

        _wasInAction = _stateMachine.CurrentStateId == CharacterStateType.Action;
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
        if (!_input.HasMoveIntent)
            return;

        if (!_actionExecutor.CanCancelByMovement)
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
            _intentBuffer.Buffer(intent);
    }

    /// <summary>Locomotion 起手：经 ActionGraph Entry×Trigger 解析后交给 ActionExecutor。</summary>
    void TryStartFromLocomotion(GameplayIntentType intent)
    {
        _intentBuffer.ClearBuffer(intent);

        var request = new ActionRequest(intent);
        var context = new ActionResolveContext(
            ActionResolveOrigin.LocomotionStart,
            null,
            _actorRoot,
            _startContext);

        if (!_resolverService.TryResolveStart(in request, in context, out ActionResolveResult resolveResult))
            return;

        if (!_actionExecutor.TryStart(in resolveResult))
            return;

        _stateMachine.TryChangeState(CharacterStateType.Action);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>角色招式输入路由：离散输入起手/缓冲、移动取消与离开 Action 后的预输入消费；玩家与敌人共用。</summary>
public sealed class CharacterActionDriver
{
    readonly HashSet<string> _registeredInputIds = new(StringComparer.Ordinal);
    readonly ICharacterInputSource inputSource;
    readonly CombatTargetLock targetLock;
    readonly InputManager _input;
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

    /// <summary>创建纯 C# 招式输入路由，并立即注册离散输入。</summary>
    public CharacterActionDriver(
        ICharacterInputSource source,
        InputManager input,
        CharacterStateMachine stateMachine,
        ActionExecutor actionExecutor,
        CombatModeService combatMode,
        CombatTargetLock lockState,
        ActionResolverService resolverService,
        Transform actorRoot,
        IActionStartContext startContext)
    {
        inputSource = source;
        _input = input;
        _stateMachine = stateMachine;
        _actionExecutor = actionExecutor;
        _combatMode = combatMode;
        targetLock = lockState;
        _resolverService = resolverService;
        _actorRoot = actorRoot;
        _startContext = startContext;
        InitializeInputRouting();
    }

    /// <summary>供 ActionExecutor CancelWindow 消费的输入缓冲桥接。</summary>
    public IActionInputBuffer CreateInputBufferBridge() => new InputBufferBridge(_input);

    /// <summary>InputManager 绑定后调用；须在 Start（全部 Awake 完成之后）执行。</summary>
    public void InitializeInputRouting()
    {
        inputSource.ConfigureDiscreteInputs(CollectDiscreteInputReferences());
        RegisterInputHandlers();
    }

    /// <summary>全部战斗模式 Graph Trigger 输入并集，供 InputReader 轮询。</summary>
    UnityEngine.InputSystem.InputActionReference[] CollectDiscreteInputReferences()
    {
        if (_combatMode?.Profile != null)
            return _combatMode.Profile.CollectAllInputReferences();

        return Array.Empty<UnityEngine.InputSystem.InputActionReference>();
    }

    /// <summary>每帧在 InputManager.IngestFrame 之后调用。</summary>
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

        _wasInAction = inAction;
    }

    /// <summary>注册全部模式 Graph 中 Trigger 对应的离散输入（按 inputId 去重）。</summary>
    public void RegisterInputHandlers()
    {
        if (_combatMode.Profile == null)
        {
            Debug.LogWarning("CharacterActionDriver: CombatModeProfile 未绑定，离散输入未注册。");
            return;
        }

        _registeredInputIds.Clear();

        bool hasAny = false;
        foreach (string inputId in _combatMode.Profile.EnumerateAllTriggerInputIds())
        {
            hasAny = true;
            if (!_registeredInputIds.Add(inputId))
                continue;

            _input.RegisterPressed(inputId, () => HandleDiscreteInput(inputId));
        }

        if (!hasAny)
        {
            Debug.LogWarning(
                "CharacterActionDriver: CombatModeProfile 的 ActionGraph 中无有效 Trigger，攻击/闪避输入未注册。");
        }
    }

    /// <summary>离开 Action 后尝试用缓冲的离散输入从 Locomotion 起手。</summary>
    bool TryStartFromBufferedInputs()
    {
        PlayerActionSet actionSet = _combatMode?.ActiveActionSet;
        if (actionSet == null)
            return false;

        foreach (string inputId in actionSet.EnumerateTriggerInputIds())
        {
            if (!_input.HasBuffer(inputId))
                continue;

            TryStartFromLocomotion(inputId);
            return _stateMachine.CurrentStateId == CharacterStateType.Action;
        }

        return false;
    }

    void ClearAllActionBuffers()
    {
        if (_combatMode?.Profile == null)
            return;

        foreach (string inputId in _combatMode.Profile.EnumerateAllTriggerInputIds())
            _input.ClearBuffer(inputId);
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

    void HandleDiscreteInput(string inputId)
    {
        if (_stateMachine.CurrentStateId == CharacterStateType.Locomotion)
            TryStartFromLocomotion(inputId);
        else if (_stateMachine.CurrentStateId == CharacterStateType.Action)
            _input.Buffer(inputId);
    }

    /// <summary>Locomotion 起手：经 ActionGraph Entry×Trigger 解析后交给 ActionExecutor。</summary>
    void TryStartFromLocomotion(string inputId)
    {
        _input.ClearBuffer(inputId);

        var request = new ActionRequest(inputId);
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

    /// <summary>将 InputManager 缓冲桥接给 ActionExecutor Cancel 消费。</summary>
    sealed class InputBufferBridge : IActionInputBuffer
    {
        readonly InputManager _inputManager;

        public InputBufferBridge(InputManager inputManager) => _inputManager = inputManager;

        public bool HasBuffer(string inputId) => _inputManager.HasBuffer(inputId);

        public bool TryConsumeBuffer(string inputId) => _inputManager.TryConsumeBuffer(inputId);
    }
}

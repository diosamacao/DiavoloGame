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
    readonly ActionRuntimeController _actionRuntime;
    /// <summary>同物体 CombatModeController；用具体类型访问 Profile / TrySetMode 三参 overload。</summary>
    readonly CombatModeController _combatMode;
    bool _wasInAction;

    /// <summary>创建纯 C# 招式输入路由，并立即注册离散输入。</summary>
    public CharacterActionDriver(
        ICharacterInputSource source,
        InputManager input,
        CharacterStateMachine stateMachine,
        ActionRuntimeController actionRuntime,
        CombatModeController combatMode,
        CombatTargetLock lockState)
    {
        inputSource = source;
        _input = input;
        _stateMachine = stateMachine;
        _actionRuntime = actionRuntime;
        _combatMode = combatMode;
        targetLock = lockState;
        InitializeInputRouting();
    }

    /// <summary>供 ActionRuntime CancelWindow 消费的输入缓冲桥接。</summary>
    public IActionComboInput CreateComboInputBridge() => new ComboInputBridge(_input);

    /// <summary>InputManager 绑定后调用；须在 Start（全部 Awake 完成之后）执行。</summary>
    public void InitializeInputRouting()
    {
        inputSource.ConfigureDiscreteInputs(_actionRuntime.GetEntryInputReferences());

        RegisterInputHandlers();
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

    /// <summary>注册全部战斗模式出招表中的离散输入（并集，按 inputId 去重）。</summary>
    public void RegisterInputHandlers()
    {
        if (_combatMode.Profile == null)
        {
            Debug.LogWarning("CharacterActionDriver: CombatModeProfile 未绑定，离散输入未注册。");
            return;
        }

        _registeredInputIds.Clear();

        bool hasAnyEntry = false;
        foreach (ActionEntry entry in _combatMode.Profile.EnumerateAllActionEntries())
        {
            if (!entry.IsValid)
                continue;

            hasAnyEntry = true;
            string inputId = entry.InputId;
            if (!_registeredInputIds.Add(inputId))
                continue;

            _input.RegisterPressed(inputId, () => HandleDiscreteInput(inputId));
        }

        if (!hasAnyEntry)
        {
            Debug.LogWarning(
                "CharacterActionDriver: CombatModeProfile 中无有效 ActionEntry，攻击/闪避输入未注册。");
        }
    }

    /// <summary>离开 Action 后尝试用缓冲的离散输入从 Locomotion 起手。</summary>
    bool TryStartFromBufferedInputs()
    {
        PlayerActionSet actionSet = _combatMode?.ActiveActionSet;
        if (actionSet == null)
            return false;

        foreach (ActionEntry entry in actionSet.Entries)
        {
            if (!entry.IsValid)
                continue;

            string inputId = entry.InputId;
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

        foreach (ActionEntry entry in _combatMode.Profile.EnumerateAllActionEntries())
        {
            if (entry.IsValid)
                _input.ClearBuffer(entry.InputId);
        }
    }

    /// <summary>移动取消：在 CancelWindow(Movement) 内退回 Locomotion。</summary>
    void TryCancelActionByMovement()
    {
        if (!_input.HasMoveIntent)
            return;

        if (!_actionRuntime.CanCancelByMovement)
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

    void TryStartFromLocomotion(string inputId)
    {
        _input.ClearBuffer(inputId);

        if (!_actionRuntime.TryStartByInput(inputId))
            return;

        _stateMachine.TryChangeState(CharacterStateType.Action);
    }

    /// <summary>将 InputManager 缓冲桥接给 ActionRuntime Cancel 消费。</summary>
    sealed class ComboInputBridge : IActionComboInput
    {
        readonly InputManager _inputManager;

        public ComboInputBridge(InputManager inputManager) => _inputManager = inputManager;

        public bool HasBuffer(string inputId) => _inputManager.HasBuffer(inputId);

        public bool TryConsumeBuffer(string inputId) => _inputManager.TryConsumeBuffer(inputId);
    }

}

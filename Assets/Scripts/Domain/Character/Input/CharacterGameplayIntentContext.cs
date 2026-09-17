using System;

/// <summary>在 Character 层把角色状态、Locomotion 与 Action 转换为 Input 条件判断。</summary>
internal sealed class CharacterGameplayIntentContext
{
    readonly ICharacterStateMachine _stateMachine;
    readonly LocomotionStateMachine _locomotion;
    readonly ActionSim _actionSim;
    readonly Func<bool> _hasPerfectDodgeCounter;

    /// <summary>创建角色意图上下文；依赖均只读，不推进任何角色状态。</summary>
    public CharacterGameplayIntentContext(
        ICharacterStateMachine stateMachine,
        LocomotionStateMachine locomotion,
        ActionSim actionSim,
        Func<bool> hasPerfectDodgeCounter)
    {
        _stateMachine = stateMachine;
        _locomotion = locomotion;
        _actionSim = actionSim;
        _hasPerfectDodgeCounter = hasPerfectDodgeCounter;
    }

    /// <summary>判断给定 Input 条件是否满足当前角色上下文。</summary>
    public bool Matches(GameplayIntentCondition condition)
    {
        switch (condition)
        {
            case GameplayIntentCondition.Always:
                return true;
            case GameplayIntentCondition.IsSprinting:
                return _stateMachine != null
                    && _stateMachine.CurrentStateId == CharacterStateType.Locomotion
                    && _locomotion != null
                    && _locomotion.Phase == LocomotionPhase.Gait
                    && _locomotion.Gait == LocomotionGait.Sprint;
            case GameplayIntentCondition.IsDodging:
                ActionDefinition currentAction =
                    _actionSim?.Snapshot.Content as ActionDefinition;
                return _stateMachine != null
                    && _stateMachine.CurrentStateId == CharacterStateType.Action
                    && currentAction != null
                    && currentAction.ActionType == CombatActionType.Dodge;
            case GameplayIntentCondition.HasPerfectDodgeCounter:
                return _hasPerfectDodgeCounter != null && _hasPerfectDodgeCounter();
            default:
                return false;
        }
    }
}

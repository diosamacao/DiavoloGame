using System;
using System.Collections.Generic;

/// <summary>把原始输入生命周期与角色上下文解释为设备无关的动作意图。</summary>
public sealed class GameplayIntentProducer
{
    readonly GameplayIntentProfile _profile;
    readonly InputManager _input;
    readonly GameplayIntentBuffer _output;
    readonly CharacterStateMachine _stateMachine;
    readonly LocomotionStateMachine _locomotion;
    readonly ActionSim _actionSim;
    readonly Func<bool> _hasPerfectDodgeCounter;
    readonly InputButton[] _buttons;
    readonly Dictionary<InputButton, int> _heldFrames = new();
    readonly HashSet<InputButton> _holdIntentEmitted = new();

    /// <summary>
    /// 创建意图生产器；Profile 是物理输入映射的唯一配置源。
    /// hasPerfectDodgeCounter：武装反击缓冲时攻击键派生 PerfectDodgeAttack。
    /// </summary>
    public GameplayIntentProducer(
        GameplayIntentProfile profile,
        InputManager input,
        GameplayIntentBuffer output,
        CharacterStateMachine stateMachine,
        LocomotionStateMachine locomotion,
        ActionSim actionSim,
        Func<bool> hasPerfectDodgeCounter = null)
    {
        _profile = profile;
        _input = input;
        _output = output;
        _stateMachine = stateMachine;
        _locomotion = locomotion;
        _actionSim = actionSim;
        _hasPerfectDodgeCounter = hasPerfectDodgeCounter;
        _buttons = CollectButtons(profile);
    }

    /// <summary>先推进旧缓冲一帧，再按量化按钮生命周期输出本帧语义意图。</summary>
    public void Step()
    {
        _output.Step();
        _output.BeginFrame();
        if (_profile == null || _input == null)
            return;

        for (int i = 0; i < _buttons.Length; i++)
            ProduceForButton(_buttons[i]);
    }

    /// <summary>单个稳定按钮在一帧内依次处理按下、长按阈值和松开。</summary>
    void ProduceForButton(InputButton button)
    {
        if (_input.WasPressedThisFrame(button))
        {
            _heldFrames[button] = 0;
            _holdIntentEmitted.Remove(button);
            TryEmit(button, GameplayIntentInputPhase.Pressed, 0);
        }

        if (_input.IsPressed(button))
        {
            int held = _heldFrames.TryGetValue(button, out int previous)
                ? previous + 1
                : 1;
            _heldFrames[button] = held;

            if (!_holdIntentEmitted.Contains(button)
                && TryEmit(button, GameplayIntentInputPhase.HoldReached, held))
            {
                // 同一物理按住周期只产生一个长按语义，避免低优先级规则后续补发。
                _holdIntentEmitted.Add(button);
            }
        }

        if (_input.WasReleasedThisFrame(button))
        {
            int held = _heldFrames.TryGetValue(button, out int duration) ? duration : 0;
            TryEmit(button, GameplayIntentInputPhase.Released, held);
            _heldFrames.Remove(button);
            _holdIntentEmitted.Remove(button);
        }
    }

    /// <summary>从同一物理事件的匹配规则中选最高优先级并独占输出。</summary>
    bool TryEmit(InputButton button, GameplayIntentInputPhase phase, int heldFrames)
    {
        // Wave 3.4：反击缓冲内攻击键强制派生 PerfectDodgeAttack（盖过 Attack/DodgeAttack）
        if (phase == GameplayIntentInputPhase.Pressed
            && _hasPerfectDodgeCounter != null
            && _hasPerfectDodgeCounter()
            && ButtonMapsToAttackFamilyPressed(button))
        {
            _output.Emit(GameplayIntentType.PerfectDodgeAttack);
            return true;
        }

        GameplayIntentBinding selected = default;
        bool found = false;

        IReadOnlyList<GameplayIntentBinding> bindings = _profile.Bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            GameplayIntentBinding candidate = bindings[i];
            if (!candidate.IsValid
                || candidate.Button != button
                || candidate.Phase != phase
                || !MatchesContext(candidate.Condition))
            {
                continue;
            }

            if (phase == GameplayIntentInputPhase.HoldReached
                && heldFrames < candidate.HoldFrames)
            {
                continue;
            }

            bool higherPriority = !found || candidate.Priority > selected.Priority;
            bool moreSpecificAtSamePriority = found
                && candidate.Priority == selected.Priority
                && ConditionSpecificity(candidate.Condition) > ConditionSpecificity(selected.Condition);
            if (higherPriority || moreSpecificAtSamePriority)
            {
                selected = candidate;
                found = true;
            }
        }

        if (!found)
            return false;

        _output.Emit(selected.Intent);
        return true;
    }

    /// <summary>评估意图规则的角色状态条件；Sprint 仅指 Locomotion 的稳态 Sprint Gait。</summary>
    bool MatchesContext(GameplayIntentCondition condition)
    {
        switch (condition)
        {
            case GameplayIntentCondition.IsSprinting:
                return _stateMachine != null
                    && _stateMachine.CurrentStateId == CharacterStateType.Locomotion
                    && _locomotion != null
                    && _locomotion.Phase == LocomotionPhase.Gait
                    && _locomotion.Gait == LocomotionGait.Sprint;
            case GameplayIntentCondition.IsDodging:
                ActionDefinition currentAction = _actionSim?.Snapshot.Content as ActionDefinition;
                return _stateMachine != null
                    && _stateMachine.CurrentStateId == CharacterStateType.Action
                    && currentAction != null
                    && currentAction.ActionType == CombatActionType.Dodge;
            case GameplayIntentCondition.HasPerfectDodgeCounter:
                return _hasPerfectDodgeCounter != null && _hasPerfectDodgeCounter();
            default:
                return true;
        }
    }

    /// <summary>
    /// Profile 是否把该按钮的 Pressed 映射到攻击族意图。
    /// 用于反击缓冲强制派生，无需资产先配 PerfectDodgeAttack 绑定。
    /// </summary>
    bool ButtonMapsToAttackFamilyPressed(InputButton button)
    {
        IReadOnlyList<GameplayIntentBinding> bindings = _profile.Bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            GameplayIntentBinding binding = bindings[i];
            if (!binding.IsValid
                || binding.Button != button
                || binding.Phase != GameplayIntentInputPhase.Pressed)
            {
                continue;
            }

            if (IsAttackFamily(binding.Intent))
                return true;
        }

        return false;
    }

    /// <summary>攻击族意图：反击缓冲应劫持这些按键映射。</summary>
    static bool IsAttackFamily(GameplayIntentType intent) =>
        intent == GameplayIntentType.Attack
        || intent == GameplayIntentType.SprintAttack
        || intent == GameplayIntentType.DodgeAttack
        || intent == GameplayIntentType.PerfectDodgeAttack
        || intent == GameplayIntentType.LongPressedAttack;

    /// <summary>
    /// 相同显式优先级下：HasPerfectDodgeCounter &gt; 其它上下文 &gt; Always。
    /// </summary>
    static int ConditionSpecificity(GameplayIntentCondition condition)
    {
        switch (condition)
        {
            case GameplayIntentCondition.Always:
                return 0;
            case GameplayIntentCondition.HasPerfectDodgeCounter:
                return 2;
            default:
                return 1;
        }
    }

    /// <summary>按 Profile 首次出现顺序收集稳定按钮，避免每帧扫描重复映射。</summary>
    static InputButton[] CollectButtons(GameplayIntentProfile profile)
    {
        if (profile == null)
            return Array.Empty<InputButton>();

        var result = new List<InputButton>(profile.Bindings.Count);
        var seen = new HashSet<InputButton>();
        for (int i = 0; i < profile.Bindings.Count; i++)
        {
            GameplayIntentBinding binding = profile.Bindings[i];
            if (binding.IsValid && seen.Add(binding.Button))
                result.Add(binding.Button);
        }

        return result.ToArray();
    }
}

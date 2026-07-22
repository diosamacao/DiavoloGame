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
    readonly string[] _inputIds;
    readonly Dictionary<string, float> _heldSeconds = new(StringComparer.Ordinal);
    readonly HashSet<string> _holdIntentEmitted = new(StringComparer.Ordinal);

    /// <summary>创建意图生产器；Profile 是物理输入映射的唯一配置源。</summary>
    public GameplayIntentProducer(
        GameplayIntentProfile profile,
        InputManager input,
        GameplayIntentBuffer output,
        CharacterStateMachine stateMachine,
        LocomotionStateMachine locomotion)
    {
        _profile = profile;
        _input = input;
        _output = output;
        _stateMachine = stateMachine;
        _locomotion = locomotion;
        _inputIds = InputBindingUtils.ResolveInputIds(profile?.CollectInputReferences());
    }

    /// <summary>推进按住计时，并按优先级输出本帧语义意图。</summary>
    public void Tick(float deltaTime)
    {
        _output.BeginFrame();
        if (_profile == null || _input == null)
            return;

        for (int i = 0; i < _inputIds.Length; i++)
            ProduceForInput(_inputIds[i], deltaTime);
    }

    /// <summary>单个物理输入在一帧内依次处理按下、长按阈值和松开。</summary>
    void ProduceForInput(string inputId, float deltaTime)
    {
        if (_input.WasPressedThisFrame(inputId))
        {
            _heldSeconds[inputId] = 0f;
            _holdIntentEmitted.Remove(inputId);
            TryEmit(inputId, GameplayIntentInputPhase.Pressed, 0f);
        }

        if (_input.IsPressed(inputId))
        {
            float held = _heldSeconds.TryGetValue(inputId, out float previous)
                ? previous + Math.Max(0f, deltaTime)
                : Math.Max(0f, deltaTime);
            _heldSeconds[inputId] = held;

            if (!_holdIntentEmitted.Contains(inputId)
                && TryEmit(inputId, GameplayIntentInputPhase.HoldReached, held))
            {
                // 同一物理按住周期只产生一个长按语义，避免低优先级规则后续补发。
                _holdIntentEmitted.Add(inputId);
            }
        }

        if (_input.WasReleasedThisFrame(inputId))
        {
            float held = _heldSeconds.TryGetValue(inputId, out float duration) ? duration : 0f;
            TryEmit(inputId, GameplayIntentInputPhase.Released, held);
            _heldSeconds.Remove(inputId);
            _holdIntentEmitted.Remove(inputId);
        }
    }

    /// <summary>从同一物理事件的匹配规则中选最高优先级并独占输出。</summary>
    bool TryEmit(string inputId, GameplayIntentInputPhase phase, float heldSeconds)
    {
        GameplayIntentBinding selected = default;
        bool found = false;

        IReadOnlyList<GameplayIntentBinding> bindings = _profile.Bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            GameplayIntentBinding candidate = bindings[i];
            if (!candidate.IsValid
                || !string.Equals(candidate.InputId, inputId, StringComparison.Ordinal)
                || candidate.Phase != phase
                || !MatchesContext(candidate.Condition))
            {
                continue;
            }

            if (phase == GameplayIntentInputPhase.HoldReached
                && heldSeconds < candidate.HoldSeconds)
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
            default:
                return true;
        }
    }

    /// <summary>相同显式优先级下，上下文限定规则优先于 Always 回退。</summary>
    static int ConditionSpecificity(GameplayIntentCondition condition) =>
        condition == GameplayIntentCondition.Always ? 0 : 1;
}

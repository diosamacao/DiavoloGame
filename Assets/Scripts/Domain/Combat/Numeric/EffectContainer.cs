using System;
using System.Collections.Generic;

/// <summary>
/// Active Effect 容器：施加 Instant/Duration/Periodic，按帧推进并维护 Attribute 修饰。
/// </summary>
public sealed class EffectContainer
{
    readonly AttributeSet _attributes;
    readonly List<ActiveEffect> _active = new(8);
    Action<int> _healthDamageHandler;

    /// <summary>绑定到角色 AttributeSet。</summary>
    public EffectContainer(AttributeSet attributes)
    {
        _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
    }

    /// <summary>
    /// 负向 Health 增量走此回调（DOT）；null 时直接改 Attribute（单测无 Vitality 时）。
    /// </summary>
    public void SetHealthDamageHandler(Action<int> healthDamageMilliHandler) =>
        _healthDamageHandler = healthDamageMilliHandler;

    /// <summary>当前激活的 Duration/Periodic 列表（只读）。</summary>
    public IReadOnlyList<ActiveEffect> ActiveEffects => _active;

    /// <summary>激活数量（Debug / Snapshot）。</summary>
    public int ActiveCount => _active.Count;

    /// <summary>
    /// 施加 Effect。Instant 立即改 Base；Duration/Periodic 进入 Active 并按叠层策略处理。
    /// </summary>
    public void Apply(EffectDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (definition.DurationPolicy == EffectDurationPolicy.Instant)
        {
            ApplyDeltas(definition.InstantDeltas, stackMultiplier: 1);
            return;
        }

        int existingIndex = FindIndexById(definition.Id);
        if (existingIndex >= 0)
        {
            ApplyStackPolicy(existingIndex, definition);
            return;
        }

        ActivateNew(definition);
    }

    /// <summary>
    /// 推进 1 逻辑帧：Periodic 跳变 → 持续时间递减 → 到期移除 Modifier。
    /// 卡肉时由 NumericSystem 整体跳过。
    /// </summary>
    public void Step()
    {
        // 倒序：到期移除时不影响尚未处理的下标
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            ActiveEffect effect = _active[i];

            // Periodic 到点：按叠层乘跳变（DOT 走 Vitality）
            if (effect.TickPeriod())
                ApplyDeltas(effect.Definition.PeriodicDeltas, effect.StackCount);

            // 持续时间耗尽：卸 Modifier 并移出容器
            if (effect.TickDuration())
                RemoveAt(i);
        }
    }

    /// <summary>按 Id 查找激活实例；没有则 null。</summary>
    public ActiveEffect FindById(string id)
    {
        int index = FindIndexById(id);
        return index >= 0 ? _active[index] : null;
    }

    void ApplyStackPolicy(int existingIndex, EffectDefinition definition)
    {
        ActiveEffect existing = _active[existingIndex];

        switch (definition.StackPolicy)
        {
            case EffectStackPolicy.Replace:
                RemoveAt(existingIndex);
                ActivateNew(definition);
                break;

            case EffectStackPolicy.Refresh:
                existing.RefreshTimers();
                break;

            case EffectStackPolicy.StackCount:
                if (existing.StackCount < definition.MaxStacks)
                {
                    existing.AddStack();
                    // Duration：每层追加一套 Modifier；Periodic 靠 StackCount 乘跳伤
                    if (definition.DurationPolicy == EffectDurationPolicy.Duration)
                        AttachModifiers(existing, definition);
                }

                existing.RefreshTimers();
                break;
        }
    }

    void ActivateNew(EffectDefinition definition)
    {
        var effect = new ActiveEffect(definition);
        if (definition.DurationPolicy == EffectDurationPolicy.Duration)
            AttachModifiers(effect, definition);

        _active.Add(effect);
    }

    void AttachModifiers(ActiveEffect effect, EffectDefinition definition)
    {
        EffectModifierSpec[] specs = definition.Modifiers;
        for (int i = 0; i < specs.Length; i++)
        {
            EffectModifierSpec spec = specs[i];
            int handle = _attributes.AddModifier(spec.Attribute, spec.Op, spec.Value);
            effect.TrackModifierHandle(handle);
        }
    }

    void RemoveAt(int index)
    {
        ActiveEffect effect = _active[index];
        IReadOnlyList<int> handles = effect.ModifierHandles;
        for (int i = 0; i < handles.Count; i++)
            _attributes.RemoveModifier(handles[i]);

        _active.RemoveAt(index);
    }

    void ApplyDeltas(EffectAttributeDelta[] deltas, int stackMultiplier)
    {
        if (deltas == null || deltas.Length == 0 || stackMultiplier <= 0)
            return;

        for (int i = 0; i < deltas.Length; i++)
        {
            EffectAttributeDelta delta = deltas[i];
            long scaled = (long)delta.DeltaMilli * stackMultiplier;
            if (scaled > int.MaxValue)
                scaled = int.MaxValue;
            else if (scaled < int.MinValue)
                scaled = int.MinValue;

            // DOT：经 Vitality 扣血，禁止 Hit Reaction；正 HOT 仍直写 Base
            if (delta.Attribute == AttributeId.Health
                && scaled < 0
                && _healthDamageHandler != null)
            {
                _healthDamageHandler((int)(-scaled));
                continue;
            }

            _attributes.AddToBase(delta.Attribute, (int)scaled);
        }
    }

    int FindIndexById(string id)
    {
        for (int i = 0; i < _active.Count; i++)
        {
            if (_active[i].Definition.Id == id)
                return i;
        }

        return -1;
    }
}

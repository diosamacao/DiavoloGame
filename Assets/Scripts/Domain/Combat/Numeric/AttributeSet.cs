using System;
using System.Collections.Generic;

/// <summary>
/// 角色属性集合：每属性仅 Base/Current 一套读法；Current 经聚合后按 Max 配对钳制到 [0, Max]。
/// </summary>
public sealed class AttributeSet
{
    static readonly AttributeId[] AllIds =
    {
        AttributeId.Health,
        AttributeId.MaxHealth,
        AttributeId.Energy,
        AttributeId.MaxEnergy,
        AttributeId.EnergyRegenMilliPerFrame,
        AttributeId.Decibel,
        AttributeId.MaxDecibel,
        AttributeId.DodgeCharges,
        AttributeId.MaxDodgeCharges,
        AttributeId.DodgeRechargeFrames,
        AttributeId.Attack,
        AttributeId.Defense,
        AttributeId.OutgoingDamageMult,
        AttributeId.IncomingDamageMult,
    };

    readonly int[] _base;
    readonly int[] _current;
    readonly List<AttributeModifier> _modifiers = new(16);
    int _nextHandle = 1;

    /// <summary>创建空属性集（全 0）；通常再由 <see cref="CharacterNumericConfig"/> 灌入 Base。</summary>
    public AttributeSet()
    {
        int count = AllIds.Length;
        _base = new int[count];
        _current = new int[count];
    }

    /// <summary>读取 Base（修饰前）。</summary>
    public int GetBase(AttributeId id) => _base[Index(id)];

    /// <summary>读取聚合并钳制后的 Current。</summary>
    public int GetCurrent(AttributeId id) => _current[Index(id)];

    /// <summary>显示用整点（Current / 1000）。</summary>
    public int GetPoints(AttributeId id) => GetCurrent(id) / 1000;

    /// <summary>设置 Base 并重算该属性（及依赖它的池属性）；池属性 Base 钳在 [0, Max.Current]。</summary>
    public void SetBase(AttributeId id, int value)
    {
        if (TryGetMaxId(id, out AttributeId maxId))
        {
            int maxCurrent = Math.Max(
                0,
                ModifierAggregator.Aggregate(_base[Index(maxId)], maxId, _modifiers));
            value = Math.Clamp(value, 0, maxCurrent);
        }
        else if (IsMaxAttribute(id) || id == AttributeId.EnergyRegenMilliPerFrame
                 || id == AttributeId.DodgeRechargeFrames
                 || id == AttributeId.Attack
                 || id == AttributeId.Defense
                 || id == AttributeId.OutgoingDamageMult
                 || id == AttributeId.IncomingDamageMult)
        {
            value = Math.Max(0, value);
        }

        _base[Index(id)] = value;
        Recalculate(id);
        RecalculateDependents(id);
    }

    /// <summary>对 Base 做增量；池属性会经 Max 钳制。</summary>
    public void AddToBase(AttributeId id, int deltaMilli)
    {
        SetBase(id, GetBase(id) + deltaMilli);
    }

    /// <summary>添加修饰并返回句柄；重算目标属性。</summary>
    public int AddModifier(AttributeId attribute, ModifierOp op, int value)
    {
        int handle = _nextHandle++;
        _modifiers.Add(new AttributeModifier(handle, attribute, op, value));
        Recalculate(attribute);
        RecalculateDependents(attribute);
        return handle;
    }

    /// <summary>按句柄移除修饰；找不到则无操作。</summary>
    public bool RemoveModifier(int handle)
    {
        for (int i = 0; i < _modifiers.Count; i++)
        {
            if (_modifiers[i].Handle != handle)
                continue;

            AttributeId attribute = _modifiers[i].Attribute;
            _modifiers.RemoveAt(i);
            Recalculate(attribute);
            RecalculateDependents(attribute);
            return true;
        }

        return false;
    }

    /// <summary>将池属性 Base 钳到当前 Max（用于 Max 被修饰抬高/压低后同步）。</summary>
    public void ClampPoolBaseToMax(AttributeId poolId)
    {
        if (!TryGetMaxId(poolId, out AttributeId maxId))
            return;

        int maxCurrent = GetCurrent(maxId);
        int baseValue = GetBase(poolId);
        int clamped = Math.Clamp(baseValue, 0, maxCurrent);
        if (clamped != baseValue)
            SetBase(poolId, clamped);
        else
            Recalculate(poolId);
    }

    void Recalculate(AttributeId id)
    {
        int index = Index(id);
        int aggregated = ModifierAggregator.Aggregate(_base[index], id, _modifiers);

        if (TryGetMaxId(id, out AttributeId maxId))
        {
            // 池属性：Current ∈ [0, Max.Current]
            int maxCurrent = ModifierAggregator.Aggregate(_base[Index(maxId)], maxId, _modifiers);
            maxCurrent = Math.Max(0, maxCurrent);
            aggregated = Math.Clamp(aggregated, 0, maxCurrent);
        }
        else if (IsMaxAttribute(id))
        {
            aggregated = Math.Max(0, aggregated);
        }

        _current[index] = aggregated;
    }

    void RecalculateDependents(AttributeId changedId)
    {
        // Max 变化时同步池 Base 并重算 Current
        if (changedId == AttributeId.MaxHealth)
            ClampPoolBaseToMax(AttributeId.Health);
        else if (changedId == AttributeId.MaxEnergy)
            ClampPoolBaseToMax(AttributeId.Energy);
        else if (changedId == AttributeId.MaxDecibel)
            ClampPoolBaseToMax(AttributeId.Decibel);
        else if (changedId == AttributeId.MaxDodgeCharges)
            ClampPoolBaseToMax(AttributeId.DodgeCharges);
    }

    static bool TryGetMaxId(AttributeId poolId, out AttributeId maxId)
    {
        switch (poolId)
        {
            case AttributeId.Health:
                maxId = AttributeId.MaxHealth;
                return true;
            case AttributeId.Energy:
                maxId = AttributeId.MaxEnergy;
                return true;
            case AttributeId.Decibel:
                maxId = AttributeId.MaxDecibel;
                return true;
            case AttributeId.DodgeCharges:
                maxId = AttributeId.MaxDodgeCharges;
                return true;
            default:
                maxId = default;
                return false;
        }
    }

    static bool IsMaxAttribute(AttributeId id) =>
        id == AttributeId.MaxHealth
        || id == AttributeId.MaxEnergy
        || id == AttributeId.MaxDecibel
        || id == AttributeId.MaxDodgeCharges;

    static int Index(AttributeId id) => (int)id;
}

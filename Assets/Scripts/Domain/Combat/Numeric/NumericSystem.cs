using System;

/// <summary>
/// 角色数值中枢（GAS-lite）：AttributeSet + EffectContainer + CombatContextFlags。
/// 唯一数值权威；Cost/Grant/Buff/DOT 经 Effect，被动回能经 Step。
/// </summary>
public sealed class NumericSystem
{
    readonly CharacterNumericConfig _config;

    /// <summary>按配置灌入 Base，池属性初始为满值。</summary>
    public NumericSystem(CharacterNumericConfig config)
    {
        _config = config ?? CharacterNumericConfig.Default;
        Attributes = new AttributeSet();
        Flags = new CombatContextFlags();
        Effects = new EffectContainer(Attributes);
        ApplyConfigBases();
    }

    /// <summary>属性权威。</summary>
    public AttributeSet Attributes { get; }

    /// <summary>短时旗标权威。</summary>
    public CombatContextFlags Flags { get; }

    /// <summary>Active Effect 容器。</summary>
    public EffectContainer Effects { get; }

    /// <summary>配置（只读用途：门闩/反击默认帧）。</summary>
    public CharacterNumericConfig Config => _config;

    /// <summary>刷新接战门闩。</summary>
    public void NotifyInCombat() => Flags.SetInCombatHold(_config.CombatHoldFrames);

    /// <summary>完美闪避成功后武装反击缓冲。</summary>
    public void ArmPerfectDodgeCounter() =>
        Flags.ArmPerfectDodgeCounter(_config.PerfectDodgeCounterFrames);

    /// <summary>Counter 起手清空缓冲。</summary>
    public void ClearPerfectDodgeCounter() => Flags.ClearPerfectDodgeCounter();

    /// <summary>施加 Effect（Instant 立即生效；Duration/Periodic 进入容器）。</summary>
    public void ApplyEffect(EffectDefinition definition) => Effects.Apply(definition);

    /// <summary>
    /// 推进 1 逻辑帧：接战回能 + 旗标递减 + 闪避充能 + Effect。
    /// 卡肉（ActionSim.IsFrozen）时由调用方跳过本方法。
    /// </summary>
    public void Step()
    {
        // 先按本帧仍有效的接战门闩回能，再递减旗标（顺序不可反）
        StepEnergyRegen();
        Flags.StepHoldAndCounter();
        // 闪避充能到点 +1 次
        StepDodgeRecharge();
        // Periodic 跳变与 Duration 到期
        Effects.Step();
    }

    /// <summary>构建只读调试快照：Attribute + ActiveEffects + Flags。</summary>
    public NumericDebugSnapshot BuildDebugSnapshot()
    {
        var entries = new NumericEffectDebugEntry[Effects.ActiveCount];
        for (int i = 0; i < Effects.ActiveCount; i++)
        {
            ActiveEffect effect = Effects.ActiveEffects[i];
            entries[i] = new NumericEffectDebugEntry(
                effect.Definition.Id,
                effect.Definition.DurationPolicy,
                effect.RemainingFrames,
                effect.StackCount,
                effect.FramesUntilNextPeriod);
        }

        return new NumericDebugSnapshot(
            Attributes.GetCurrent(AttributeId.Health),
            Attributes.GetCurrent(AttributeId.Energy),
            Attributes.GetCurrent(AttributeId.Attack),
            Attributes.GetCurrent(AttributeId.Defense),
            Attributes.GetCurrent(AttributeId.OutgoingDamageMult),
            Attributes.GetCurrent(AttributeId.IncomingDamageMult),
            Flags.InCombatHoldFrames,
            Flags.PerfectDodgeCounterFrames,
            Flags.DodgeRechargeFramesLeft,
            entries);
    }

    void ApplyConfigBases()
    {
        int maxHealth = CharacterNumericConfig.ToMilli(_config.MaxHealthPoints);
        int maxEnergy = CharacterNumericConfig.ToMilli(_config.MaxEnergyPoints);
        int maxDecibel = CharacterNumericConfig.ToMilli(_config.MaxDecibelPoints);
        int maxDodge = CharacterNumericConfig.ToMilli(_config.MaxDodgeChargesPoints);

        Attributes.SetBase(AttributeId.MaxHealth, maxHealth);
        Attributes.SetBase(AttributeId.Health, maxHealth);
        Attributes.SetBase(AttributeId.MaxEnergy, maxEnergy);
        Attributes.SetBase(AttributeId.Energy, maxEnergy);
        Attributes.SetBase(AttributeId.EnergyRegenMilliPerFrame, _config.EnergyRegenMilliPerFrame);
        Attributes.SetBase(AttributeId.MaxDecibel, maxDecibel);
        Attributes.SetBase(AttributeId.Decibel, 0);
        Attributes.SetBase(AttributeId.MaxDodgeCharges, maxDodge);
        Attributes.SetBase(AttributeId.DodgeCharges, maxDodge);
        Attributes.SetBase(
            AttributeId.DodgeRechargeFrames,
            CharacterNumericConfig.ToMilli(_config.DodgeRechargeFrames));
        Attributes.SetBase(AttributeId.Attack, CharacterNumericConfig.ToMilli(_config.AttackPoints));
        Attributes.SetBase(AttributeId.Defense, CharacterNumericConfig.ToMilli(_config.DefensePoints));
        // 倍率默认 ×1.0（1000 milli）
        Attributes.SetBase(AttributeId.OutgoingDamageMult, 1000);
        Attributes.SetBase(AttributeId.IncomingDamageMult, 1000);
    }

    void StepEnergyRegen()
    {
        if (!Flags.IsInCombat)
            return;

        int regen = Attributes.GetCurrent(AttributeId.EnergyRegenMilliPerFrame);
        if (regen <= 0)
            return;

        int energy = Attributes.GetBase(AttributeId.Energy);
        int maxEnergy = Attributes.GetCurrent(AttributeId.MaxEnergy);
        if (energy >= maxEnergy)
            return;

        Attributes.SetBase(AttributeId.Energy, Math.Min(maxEnergy, energy + regen));
    }

    void StepDodgeRecharge()
    {
        int charges = Attributes.GetBase(AttributeId.DodgeCharges);
        int maxCharges = Attributes.GetCurrent(AttributeId.MaxDodgeCharges);
        if (charges >= maxCharges)
        {
            Flags.SetDodgeRechargeFramesLeft(0);
            return;
        }

        if (Flags.DodgeRechargeFramesLeft <= 0)
            return;

        if (!Flags.TickDodgeRecharge())
            return;

        Attributes.SetBase(
            AttributeId.DodgeCharges,
            Math.Min(maxCharges, charges + CharacterNumericConfig.ToMilli(1)));

        if (Attributes.GetBase(AttributeId.DodgeCharges) < maxCharges)
        {
            int rechargeFrames = Math.Max(1, Attributes.GetPoints(AttributeId.DodgeRechargeFrames));
            Flags.SetDodgeRechargeFramesLeft(rechargeFrames);
        }
    }

    /// <summary>
    /// 消耗 1 次闪避并刷新接战门闩；不足时返回 false。
    /// 前置：DodgeCharges Points &gt; 0。
    /// </summary>
    public bool TryConsumeDodgeCharge()
    {
        if (Attributes.GetPoints(AttributeId.DodgeCharges) <= 0)
            return false;

        Attributes.AddToBase(AttributeId.DodgeCharges, -CharacterNumericConfig.ToMilli(1));
        if (Attributes.GetBase(AttributeId.DodgeCharges) < Attributes.GetCurrent(AttributeId.MaxDodgeCharges)
            && Flags.DodgeRechargeFramesLeft <= 0)
        {
            int rechargeFrames = Math.Max(1, Attributes.GetPoints(AttributeId.DodgeRechargeFrames));
            Flags.SetDodgeRechargeFramesLeft(rechargeFrames);
        }

        NotifyInCombat();
        return true;
    }
}

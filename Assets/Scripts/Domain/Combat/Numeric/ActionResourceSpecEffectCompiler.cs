using System;

/// <summary>
/// 将 <see cref="ActionResourceSpec"/> 编译为对 Numeric 的 Instant Cost/Grant（禁止 Spec 直写以外的旁路）。
/// </summary>
public static class ActionResourceSpecEffectCompiler
{
    /// <summary>只读：是否负担得起价签门槛。</summary>
    public static bool CanAfford(NumericSystem numeric, ActionResourceSpec spec)
    {
        if (numeric == null)
            throw new ArgumentNullException(nameof(numeric));
        if (spec == null)
            return true;

        AttributeSet attrs = numeric.Attributes;
        if (spec.EnergyCost > 0 && attrs.GetPoints(AttributeId.Energy) < spec.EnergyCost)
            return false;

        if (spec.ConsumeDodgeCharge && attrs.GetPoints(AttributeId.DodgeCharges) <= 0)
            return false;

        if (spec.RequiresDecibelFull
            && attrs.GetPoints(AttributeId.Decibel) < attrs.GetPoints(AttributeId.MaxDecibel))
        {
            return false;
        }

        return true;
    }

    /// <summary>起手扣费：编译并立即 Apply Instant Cost Effect；调用前须 CanAfford。</summary>
    public static void ApplyCost(NumericSystem numeric, ActionResourceSpec spec)
    {
        if (numeric == null)
            throw new ArgumentNullException(nameof(numeric));
        if (spec == null)
            return;

        if (spec.EnergyCost > 0)
        {
            numeric.ApplyEffect(EffectDefinition.CreateInstant(
                "Cost.Energy",
                new EffectAttributeDelta(
                    AttributeId.Energy,
                    -CharacterNumericConfig.ToMilli(spec.EnergyCost))));
        }

        if (spec.ConsumeDodgeCharge)
            numeric.TryConsumeDodgeCharge();

        if (spec.ClearsDecibelOnStart)
        {
            int decibel = numeric.Attributes.GetBase(AttributeId.Decibel);
            if (decibel > 0)
            {
                numeric.ApplyEffect(EffectDefinition.CreateInstant(
                    "Cost.ClearDecibel",
                    new EffectAttributeDelta(AttributeId.Decibel, -decibel)));
            }
        }

        // ConsumeDodgeCharge 已 NotifyInCombat；其余起手也刷门闩
        if (!spec.ConsumeDodgeCharge)
            numeric.NotifyInCombat();
    }

    /// <summary>ConfirmHit 回填：编译 Instant Grant；挥空不得调用。</summary>
    public static void ApplyGrant(NumericSystem numeric, ActionResourceSpec spec)
    {
        if (numeric == null)
            throw new ArgumentNullException(nameof(numeric));
        if (spec == null)
            return;

        if (spec.EnergyGrantOnHit > 0)
        {
            numeric.ApplyEffect(EffectDefinition.CreateInstant(
                "Grant.Energy",
                new EffectAttributeDelta(
                    AttributeId.Energy,
                    CharacterNumericConfig.ToMilli(spec.EnergyGrantOnHit))));
        }

        if (spec.DecibelGrantOnHit > 0)
        {
            numeric.ApplyEffect(EffectDefinition.CreateInstant(
                "Grant.Decibel",
                new EffectAttributeDelta(
                    AttributeId.Decibel,
                    CharacterNumericConfig.ToMilli(spec.DecibelGrantOnHit))));
        }

        if (spec.EnergyGrantOnHit > 0 || spec.DecibelGrantOnHit > 0)
            numeric.NotifyInCombat();
    }
}

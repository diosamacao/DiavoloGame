/// <summary>
/// 命中伤害入口；G4 起委托 <see cref="DamageNumericCalculator"/>。
/// 无攻防 Numeric 时退化为 HitPayload.BaseDamage。
/// </summary>
public static class CombatDamageCalculator
{
    /// <summary>使用攻击者/防御者 Numeric 计算非负伤害。</summary>
    public static float Calculate(
        in ActionHitContext context,
        NumericSystem attacker,
        NumericSystem defender)
    {
        float baseDamage = context.Hitbox != null ? context.Hitbox.Payload.BaseDamage : 0f;
        return DamageNumericCalculator.Calculate(attacker, defender, baseDamage);
    }

    /// <summary>无 Numeric 上下文时的扁平伤害（木桩/测试回退）。</summary>
    public static float Calculate(in ActionHitContext context)
    {
        if (context.Hitbox == null)
            return 0f;
        return DamageNumericCalculator.Calculate(null, null, context.Hitbox.Payload.BaseDamage);
    }
}

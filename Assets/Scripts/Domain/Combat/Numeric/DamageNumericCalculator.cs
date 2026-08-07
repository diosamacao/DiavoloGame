using System;

/// <summary>
/// G4 伤害公式：框系数 × Attack/基准 × 出伤倍率 × 防御减伤 × 承伤倍率；全程 milli-int。
/// </summary>
public static class DamageNumericCalculator
{
    /// <summary>公式参考攻击力（10 点）；使 BaseDamage=10 且 Attack=10 时基线仍为 10 伤。</summary>
    public const int ReferenceAttackMilli = 10_000;

    /// <summary>防御常数 K（100 点 = 100000 milli）。</summary>
    public const int DefenseConstantMilli = 100_000;

    /// <summary>
    /// 计算最终伤害（显示点）。attacker/defender 为 null 时退化为纯 BaseDamage。
    /// </summary>
    public static float Calculate(
        NumericSystem attacker,
        NumericSystem defender,
        float baseDamage)
    {
        int finalMilli = CalculateMilli(attacker, defender, baseDamage);
        return finalMilli / 1000f;
    }

    /// <summary>计算最终伤害 milli；供单测金值比对。</summary>
    public static int CalculateMilli(
        NumericSystem attacker,
        NumericSystem defender,
        float baseDamage)
    {
        if (baseDamage <= 0f)
            return 0;

        long scaleMilli = (long)Math.Round(baseDamage * 1000.0);
        if (scaleMilli <= 0)
            return 0;

        // 无 Numeric 时保持旧扁平伤
        if (attacker == null && defender == null)
            return (int)scaleMilli;

        long attack = attacker != null
            ? Math.Max(0, attacker.Attributes.GetCurrent(AttributeId.Attack))
            : ReferenceAttackMilli;
        long defense = defender != null
            ? Math.Max(0, defender.Attributes.GetCurrent(AttributeId.Defense))
            : 0;
        long outgoing = attacker != null
            ? Math.Max(1, attacker.Attributes.GetCurrent(AttributeId.OutgoingDamageMult))
            : 1000;
        long incoming = defender != null
            ? Math.Max(1, defender.Attributes.GetCurrent(AttributeId.IncomingDamageMult))
            : 1000;

        // raw = BaseDamage × (Attack / RefAttack) × Outgoing
        long raw = scaleMilli * attack / ReferenceAttackMilli;
        raw = raw * outgoing / 1000;

        // afterDef = raw × K / (Defense + K)
        long afterDef = defense <= 0
            ? raw
            : raw * DefenseConstantMilli / (defense + DefenseConstantMilli);

        long finalMilli = afterDef * incoming / 1000;
        if (finalMilli <= 0)
            return 0;

        // 非零命中至少 1 点
        if (finalMilli < 1000)
            return 1000;

        if (finalMilli > int.MaxValue)
            return int.MaxValue;
        return (int)finalMilli;
    }
}

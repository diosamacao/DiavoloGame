using System.Collections.Generic;

/// <summary>
/// 无状态聚合：Current = (Base + ΣFlat) × Π(Percent/1000)；无 Percent 时乘区为 1。
/// </summary>
public static class ModifierAggregator
{
    /// <summary>对指定属性聚合 Base 与修饰列表，返回未做 Max 钳制的 Current。</summary>
    public static int Aggregate(int baseValue, AttributeId attribute, IReadOnlyList<AttributeModifier> modifiers)
    {
        long flatSum = 0;
        long percentProductMilli = 1000; // 1.0
        bool hasPercent = false;

        for (int i = 0; i < modifiers.Count; i++)
        {
            AttributeModifier mod = modifiers[i];
            if (mod.Attribute != attribute)
                continue;

            if (mod.Op == ModifierOp.Flat)
            {
                flatSum += mod.Value;
                continue;
            }

            // Percent：连乘 milli 因子，避免浮点
            hasPercent = true;
            percentProductMilli = percentProductMilli * mod.Value / 1000;
        }

        long raw = baseValue + flatSum;
        if (hasPercent)
            raw = raw * percentProductMilli / 1000;

        if (raw > int.MaxValue)
            return int.MaxValue;
        if (raw < int.MinValue)
            return int.MinValue;
        return (int)raw;
    }
}

/// <summary>战斗伤害纯计算入口；把招式基础值与当前判定框倍率合成为最终伤害。</summary>
public static class CombatDamageCalculator
{
    /// <summary>计算一次命中的非负伤害值。</summary>
    public static float Calculate(in ActionHitContext context)
    {
        if (context.Action == null || context.Hitbox == null)
            return 0f;

        return context.Action.BaseDamage * context.Hitbox.DamageWeight;
    }
}

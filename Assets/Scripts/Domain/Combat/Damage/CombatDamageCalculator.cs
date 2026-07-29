/// <summary>战斗伤害纯计算入口；伤害唯一来自当前 Hitbox 的结算载荷。</summary>
public static class CombatDamageCalculator
{
    /// <summary>计算一次命中的非负伤害值。</summary>
    public static float Calculate(in ActionHitContext context)
    {
        if (context.Hitbox == null)
            return 0f;

        return context.Hitbox.Payload.BaseDamage;
    }
}

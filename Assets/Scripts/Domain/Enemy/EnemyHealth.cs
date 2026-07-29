/// <summary>敌人生命值类型；沿用角色通用扣血语义并为敌人装配提供明确依赖。</summary>
public sealed class EnemyHealth : CharacterHealth
{
    /// <summary>创建满血敌人生命值。</summary>
    public EnemyHealth(float maxHealth)
        : base(maxHealth)
    {
    }
}

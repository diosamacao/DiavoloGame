using System;
using UnityEngine;

/// <summary>角色通用生命值模型；只处理扣血与死亡边沿，不依赖 App 架构。</summary>
public class CharacterHealth
{
    /// <summary>创建满血角色生命值。</summary>
    public CharacterHealth(float maxHealth)
    {
        MaxHealth = Mathf.Max(1f, maxHealth);
        CurrentHealth = MaxHealth;
    }

    /// <summary>最大生命值。</summary>
    public float MaxHealth { get; }

    /// <summary>当前生命值。</summary>
    public float CurrentHealth { get; private set; }

    /// <summary>生命值是否已归零。</summary>
    public bool IsDead => CurrentHealth <= 0f;

    /// <summary>成功受到非致命伤害后触发。</summary>
    public event Action<ActionHitContext, float> Damaged;

    /// <summary>收到非致命命中后触发；即使本次伤害为 0，也用于播放受击反应。</summary>
    public event Action<ActionHitContext> HitReceived;

    /// <summary>生命值首次归零时触发。</summary>
    public event Action<ActionHitContext, float> Died;

    /// <summary>应用一次命中；扣血与受击反应分离，0 伤害仍可触发 Hit。</summary>
    public void ApplyDamage(float damage, in ActionHitContext context)
    {
        if (IsDead)
            return;

        float applied = Mathf.Min(CurrentHealth, Mathf.Max(0f, damage));
        if (applied > 0f)
            CurrentHealth -= applied;

        if (IsDead)
        {
            Died?.Invoke(context, applied);
            return;
        }

        HitReceived?.Invoke(context);
        if (applied > 0f)
            Damaged?.Invoke(context, applied);
    }
}

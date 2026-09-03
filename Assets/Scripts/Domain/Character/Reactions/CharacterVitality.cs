using System;
using UnityEngine;

/// <summary>
/// Health Attribute 边沿服务：扣血与 Hit/Death 事件；数值权威在 <see cref="NumericSystem"/>。
/// </summary>
public sealed class CharacterVitality
{
    readonly NumericSystem _numeric;
    VitalityReplicationEdge _replicationEdge;

    /// <summary>绑定 Numeric；事件供 Reaction 订阅。</summary>
    public CharacterVitality(NumericSystem numeric)
    {
        _numeric = numeric ?? throw new ArgumentNullException(nameof(numeric));
    }

    /// <summary>所属数值中枢。</summary>
    public NumericSystem Numeric => _numeric;

    /// <summary>当前生命（显示点 = milli/1000）。</summary>
    public float CurrentHealth => _numeric.Attributes.GetCurrent(AttributeId.Health) / 1000f;

    /// <summary>最大生命（显示点）。</summary>
    public float MaxHealth => _numeric.Attributes.GetCurrent(AttributeId.MaxHealth) / 1000f;

    /// <summary>生命是否已归零。</summary>
    public bool IsDead => _numeric.Attributes.GetCurrent(AttributeId.Health) <= 0;

    /// <summary>本逻辑步生命边沿；Step 开头清零，供复制快照读取。</summary>
    public VitalityReplicationEdge ReplicationEdge => _replicationEdge;

    /// <summary>最近一次 ConfirmHitReaction 的档位；供复制命中与 HUD。</summary>
    public HitReactionKind LastConfirmedReactionKind { get; private set; }

    /// <summary>新逻辑步开始时清边沿，避免上一帧 Hit/Death 被重复复制。</summary>
    public void ClearReplicationEdge() => _replicationEdge = VitalityReplicationEdge.None;

    /// <summary>成功受到非致命伤害后触发。</summary>
    public event Action<ActionHitContext, float> Damaged;

    /// <summary>收到非致命命中后触发；0 伤仍可播受击。</summary>
    public event Action<ActionHitContext> HitReceived;

    /// <summary>生命首次归零时触发。</summary>
    public event Action<ActionHitContext, float> Died;

    /// <summary>
    /// 客机用权威快照覆盖当前生命；不发 Hit/Death 事件（边沿由房间调 EnterHit）。
    /// </summary>
    public void ApplyAuthorityHealthMilli(int healthMilli)
    {
        int max = _numeric.Attributes.GetCurrent(AttributeId.MaxHealth);
        int clamped = healthMilli < 0 ? 0 : healthMilli;
        if (max > 0 && clamped > max)
            clamped = max;
        _numeric.Attributes.SetBase(AttributeId.Health, clamped);
    }

    /// <summary>
    /// 将 Max/Current Health 设为指定整点（敌人 Definition.MaxHp 覆盖 Config）。
    /// </summary>
    public void ResetMaxHealthPoints(int maxHealthPoints)
    {
        int milli = CharacterNumericConfig.ToMilli(Mathf.Max(1, maxHealthPoints));
        _numeric.Attributes.SetBase(AttributeId.MaxHealth, milli);
        _numeric.Attributes.SetBase(AttributeId.Health, milli);
    }

    /// <summary>
    /// Service 裁定后回写复制边沿。Flinch / None 不得标 Hit，避免 Observer 把底轨当受击重播。
    /// </summary>
    public void ConfirmHitReaction(HitReactionKind kind)
    {
        LastConfirmedReactionKind = kind;
        if (IsDead || kind == HitReactionKind.Death)
        {
            _replicationEdge = VitalityReplicationEdge.Death;
            return;
        }

        _replicationEdge = kind >= HitReactionKind.LightStun
            ? VitalityReplicationEdge.Hit
            : VitalityReplicationEdge.None;
    }

    /// <summary>应用一次命中；扣血与受击反应分离，0 伤害仍可触发 Hit。</summary>
    public void ApplyDamage(float damage, in ActionHitContext context) =>
        ApplyDamageMilli(
            Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0f, damage) * 1000f)),
            in context,
            triggerHitReaction: true);

    /// <summary>
    /// Periodic DOT 扣血：不触发 Hit Reaction / Damaged；归零仍触发 Died。
    /// 供 EffectContainer 回调；damageMilli 为非负伤害量。
    /// </summary>
    public void ApplyPeriodicHealthDamageMilli(int damageMilli) =>
        ApplyDamageMilli(Mathf.Max(0, damageMilli), default, triggerHitReaction: false);

    void ApplyDamageMilli(int damageMilli, in ActionHitContext context, bool triggerHitReaction)
    {
        if (IsDead || damageMilli <= 0)
        {
            // 0 伤命中仍可播受击；边沿只由 ConfirmHitReaction 写入。
            if (triggerHitReaction && !IsDead && damageMilli <= 0)
                HitReceived?.Invoke(context);
            return;
        }

        int healthBefore = _numeric.Attributes.GetBase(AttributeId.Health);
        int appliedMilli = Math.Min(healthBefore, damageMilli);
        if (appliedMilli > 0)
            _numeric.Attributes.AddToBase(AttributeId.Health, -appliedMilli);

        float applied = appliedMilli / 1000f;
        if (IsDead)
        {
            _replicationEdge = VitalityReplicationEdge.Death;
            Died?.Invoke(context, applied);
            return;
        }

        if (!triggerHitReaction)
            return;

        HitReceived?.Invoke(context);
        if (appliedMilli > 0)
            Damaged?.Invoke(context, applied);
    }
}

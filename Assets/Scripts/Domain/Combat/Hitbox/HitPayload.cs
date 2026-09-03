using System;
using UnityEngine;

/// <summary>单个 Hitbox 的结算载荷；伤害、受击语义与命中反馈不再由动作资源持有。</summary>
[Serializable]
public sealed class HitPayload
{
    [Tooltip("该判定框每次有效命中的基础伤害。")]
    [SerializeField] float baseDamage = 10f;
    [Tooltip("交给目标上层 ReactionResolver 的语义 Id；空值使用目标默认受击规则。")]
    [SerializeField] string hitReactionId = string.Empty;
    [Tooltip("打断等级；旧资产缺省 1。")]
    [SerializeField] int interruptLevel = HitReactionResolveQuery.DefaultInterruptLevel;
    [Tooltip("这刀期望的受击档；旧资产缺省 LightStun（仍进 Hit）。轻击测 Shake 时改为 Flinch。")]
    [SerializeField] HitReactionKind desiredReaction = HitReactionKind.LightStun;
    [SerializeField] HitFeedbackSettings feedback = new();

    /// <summary>Unity 序列化与 `new HitPayload()` 默认旧盒子语义。</summary>
    public HitPayload()
    {
    }

    /// <summary>该判定框造成的非负伤害。</summary>
    public float BaseDamage => Mathf.Max(0f, baseDamage);

    /// <summary>供目标上层选择受击动作的语义 Id。</summary>
    public string HitReactionId => hitReactionId ?? string.Empty;

    /// <summary>本刀打断等级；未填或非法时按旧盒子默认 1。</summary>
    public int InterruptLevel =>
        interruptLevel > 0 ? interruptLevel : HitReactionResolveQuery.DefaultInterruptLevel;

    /// <summary>攻击期望档；未序列化的旧盒子为 LightStun。</summary>
    public HitReactionKind DesiredReaction => desiredReaction;

    /// <summary>命中镜头、卡肉与受击 Cue（VFX/SFX）反馈。</summary>
    public HitFeedbackSettings Feedback => feedback ?? new HitFeedbackSettings();

    /// <summary>测试与代码装配用；运行时资产仍走序列化字段。</summary>
    public HitPayload(
        float baseDamage,
        int interruptLevel,
        HitReactionKind desiredReaction,
        string hitReactionId = "")
    {
        this.baseDamage = Mathf.Max(0f, baseDamage);
        this.interruptLevel = interruptLevel;
        this.desiredReaction = desiredReaction;
        this.hitReactionId = hitReactionId ?? string.Empty;
        feedback = new HitFeedbackSettings();
    }
}

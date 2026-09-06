using System;
using UnityEngine;

/// <summary>单个 Hitbox 的结算载荷：真伤数值、被弹政策/选片 Id，以及命中反馈。</summary>
[Serializable]
public sealed class HitPayload
{
    [Tooltip("该判定框每次有效命中的基础伤害。")]
    [SerializeField] float baseDamage = 10f;
    [Tooltip("交给目标上层 ReactionResolver 的语义 Id；空值使用目标默认受击规则。")]
    [SerializeField] string hitReactionId = string.Empty;
    [Tooltip("本刀冲击力。与目标韧性比较后裁定档位；旧资产缺省 1。")]
    [SerializeField] int interruptLevel = HitReactionResolveQuery.DefaultInterruptLevel;
    [Tooltip("被弹刀后攻击者是否断招。Interrupt=进 Hit；Continue=原招继续。不读冲击力。")]
    [SerializeField] ParriedActionPolicy parriedActionPolicy = ParriedActionPolicy.Interrupt;
    [Tooltip("查攻击者 CharacterReactionSet 的 Parried 规则 Id，不是对方 Hit。空则默认 Parried 片。")]
    [SerializeField] string parriedReactionId = string.Empty;
    [SerializeField] HitFeedbackSettings feedback = new();

    /// <summary>Unity 序列化与 `new HitPayload()` 默认旧盒子语义。</summary>
    public HitPayload()
    {
    }

    /// <summary>该判定框造成的非负伤害。</summary>
    public float BaseDamage => Mathf.Max(0f, baseDamage);

    /// <summary>供目标上层选择受击动作的语义 Id。</summary>
    public string HitReactionId => hitReactionId ?? string.Empty;

    /// <summary>本刀冲击力；未填或非法时按旧盒子默认 1。</summary>
    public int InterruptLevel =>
        interruptLevel > 0 ? interruptLevel : HitReactionResolveQuery.DefaultInterruptLevel;

    /// <summary>被弹刀后攻击者是否进 Hit；默认 Interrupt。</summary>
    public ParriedActionPolicy ParriedActionPolicy => parriedActionPolicy;

    /// <summary>攻击者 Parried 选片 Id；空串走默认 Parried。</summary>
    public string ParriedReactionId => parriedReactionId ?? string.Empty;

    /// <summary>命中镜头、卡肉与受击 Cue（VFX/SFX）反馈。</summary>
    public HitFeedbackSettings Feedback => feedback ?? new HitFeedbackSettings();

    /// <summary>未填冲击力时写成 1。</summary>
    public bool EnsureInterruptLevelDefault()
    {
        if (interruptLevel > 0)
            return false;

        interruptLevel = HitReactionResolveQuery.DefaultInterruptLevel;
        return true;
    }

    /// <summary>测试与代码装配用；运行时资产仍走序列化字段。</summary>
    public HitPayload(
        float baseDamage,
        int interruptLevel,
        string hitReactionId = "",
        ParriedActionPolicy parriedActionPolicy = ParriedActionPolicy.Interrupt,
        string parriedReactionId = "")
    {
        this.baseDamage = Mathf.Max(0f, baseDamage);
        this.interruptLevel = interruptLevel;
        this.hitReactionId = hitReactionId ?? string.Empty;
        this.parriedActionPolicy = parriedActionPolicy;
        this.parriedReactionId = parriedReactionId ?? string.Empty;
        feedback = new HitFeedbackSettings();
    }
}

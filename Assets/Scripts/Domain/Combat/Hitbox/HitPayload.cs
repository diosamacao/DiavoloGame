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
    [SerializeField] HitFeedbackSettings feedback = new();

    /// <summary>该判定框造成的非负伤害。</summary>
    public float BaseDamage => Mathf.Max(0f, baseDamage);

    /// <summary>供目标上层选择受击动作的语义 Id。</summary>
    public string HitReactionId => hitReactionId ?? string.Empty;

    /// <summary>命中镜头与卡肉反馈。</summary>
    public HitFeedbackSettings Feedback => feedback ?? new HitFeedbackSettings();
}

using UnityEngine;

/// <summary>一次命中判定的上下文，供 Hurtbox 侧消费。</summary>
public readonly struct ActionHitContext
{
    public ActionHitContext(ActionDefinition action, HitboxKeyframe hitbox, Transform attacker)
    {
        Action = action;
        Hitbox = hitbox;
        Attacker = attacker;
    }

    public ActionDefinition Action { get; }
    public HitboxKeyframe Hitbox { get; }
    public Transform Attacker { get; }
}

using UnityEngine;

/// <summary>一次命中判定的上下文，供 Hurtbox 侧消费。</summary>
public readonly struct ActionHitContext
{
    /// <summary>创建带招式会话身份与攻击者模拟 Id 的命中上下文。</summary>
    public ActionHitContext(
        ActionDefinition action,
        HitboxNotifyState hitbox,
        Transform attacker,
        int actionInstanceId,
        SimActorId attackerId)
    {
        Action = action;
        Hitbox = hitbox;
        Attacker = attacker;
        ActionInstanceId = actionInstanceId;
        AttackerId = attackerId;
    }

    /// <summary>产生本次命中的招式定义。</summary>
    public ActionDefinition Action { get; }

    /// <summary>产生本次命中的 Hitbox 时间轴窗口。</summary>
    public HitboxNotifyState Hitbox { get; }

    /// <summary>攻击者表现根节点，仅供几何与帧末表现桥接。</summary>
    public Transform Attacker { get; }

    /// <summary>攻击者本次招式会话编号，用于表现侧一次性反馈去重。</summary>
    public int ActionInstanceId { get; }

    /// <summary>攻击者模拟身份；供 Hurtbox 查找 Numeric 攻防。</summary>
    public SimActorId AttackerId { get; }
}

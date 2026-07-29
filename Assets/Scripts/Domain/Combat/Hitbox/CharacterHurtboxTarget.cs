using UnityEngine;

/// <summary>纯 C# 角色受击目标；连接 Hurtbox、阵营与生命值模型。</summary>
public sealed class CharacterHurtboxTarget : ITargetable
{
    readonly Transform _root;
    readonly Transform _aimTransform;
    readonly HurtboxDefinition _hurtbox;
    readonly CharacterHealth _health;

    /// <summary>创建随角色根节点移动的受击目标。</summary>
    public CharacterHurtboxTarget(
        Transform root,
        Transform aimTransform,
        int teamId,
        HurtboxDefinition hurtbox,
        CharacterHealth health)
    {
        _root = root;
        _aimTransform = aimTransform != null ? aimTransform : root;
        TeamId = teamId;
        _hurtbox = hurtbox ?? new HurtboxDefinition();
        _health = health;
    }

    /// <summary>角色根实例 id，用于同一招去重。</summary>
    public int TargetInstanceId => _root != null ? _root.gameObject.GetInstanceID() : 0;

    /// <summary>角色受击根节点。</summary>
    public Transform TargetTransform => _root;

    /// <summary>索敌瞄准点。</summary>
    public Transform AimTransform => _aimTransform;

    /// <summary>根节点有效且生命值未归零时可被索敌。</summary>
    public bool IsAlive =>
        _root != null
        && _root.gameObject.activeInHierarchy
        && _health != null
        && !_health.IsDead;

    /// <summary>当前生命值。</summary>
    public float CurrentHealth => _health != null ? _health.CurrentHealth : 0f;

    /// <summary>角色阵营 id。</summary>
    public int TeamId { get; }

    /// <summary>返回随根节点变换后的世界空间受击框。</summary>
    public HitboxOrientedBox GetWorldHurtbox() =>
        HitboxMath.BuildFromHurtbox(_root, _hurtbox);

    /// <summary>把命中上下文换算为伤害并交给生命值模型。</summary>
    public void OnHit(in ActionHitContext context)
    {
        if (!IsAlive)
            return;

        _health.ApplyDamage(CombatDamageCalculator.Calculate(in context), in context);
    }
}

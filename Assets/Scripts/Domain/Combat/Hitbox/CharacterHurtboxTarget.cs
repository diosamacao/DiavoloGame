using System;
using UnityEngine;

/// <summary>纯 C# 角色受击目标；Hurtbox 由 MotorSim 逻辑根位姿构建。</summary>
public sealed class CharacterHurtboxTarget : ITargetable
{
    readonly Transform _root;
    readonly Transform _aimTransform;
    readonly HurtboxDefinition _hurtbox;
    readonly CharacterHealth _health;
    readonly Func<SimActorId> _simulationIdProvider;
    readonly CharacterMotorSim _motorSim;

    /// <summary>创建随逻辑电机移动的受击目标。</summary>
    public CharacterHurtboxTarget(
        Transform root,
        Transform aimTransform,
        int teamId,
        HurtboxDefinition hurtbox,
        CharacterHealth health,
        Func<SimActorId> simulationIdProvider,
        CharacterMotorSim motorSim)
    {
        _root = root;
        _aimTransform = aimTransform != null ? aimTransform : root;
        TeamId = teamId;
        _hurtbox = hurtbox ?? new HurtboxDefinition();
        _health = health;
        _simulationIdProvider = simulationIdProvider;
        _motorSim = motorSim ?? throw new ArgumentNullException(nameof(motorSim));
    }

    /// <summary>角色在 SimulationWorld 内的稳定身份。</summary>
    public SimActorId SimulationId => _simulationIdProvider?.Invoke() ?? SimActorId.Invalid;

    /// <summary>角色受击根节点（索敌/表现）；命中几何不读其世界矩阵作权威。</summary>
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

    /// <summary>按 MotorSim 逻辑根构建受击框。</summary>
    public HitboxOrientedBox GetLogicalHurtbox()
    {
        float heightY = _root != null ? _root.position.y : 0f;
        SimCombatPose pose = SimCombatPose.FromMotor(_motorSim, heightY);
        return HitboxMath.BuildFromHurtboxLogical(in pose, _hurtbox);
    }

    /// <summary>把命中上下文换算为伤害并交给生命值模型。</summary>
    public void OnHit(in ActionHitContext context)
    {
        if (!IsAlive)
            return;

        _health.ApplyDamage(CombatDamageCalculator.Calculate(in context), in context);
    }
}

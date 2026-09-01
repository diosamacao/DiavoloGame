using System;
using UnityEngine;

/// <summary>纯 C# 角色受击目标；Hurtbox 由 MotorSim 逻辑根位姿构建；伤害写 Numeric Health。</summary>
public sealed class CharacterHurtboxTarget : ITargetable, IHitAbsorbQuery
{
    readonly Transform _root;
    readonly Transform _aimTransform;
    readonly HurtboxDefinition _hurtbox;
    readonly CharacterVitality _vitality;
    readonly ActionSim _actionSim;
    readonly Func<SimActorId> _simulationIdProvider;
    readonly Func<bool> _participationProvider;
    Func<SimActorId, NumericSystem> _numericLookup;
    readonly CharacterMotorSim _motorSim;

    /// <summary>创建随逻辑电机移动的受击目标。</summary>
    public CharacterHurtboxTarget(
        Transform root,
        Transform aimTransform,
        int teamId,
        HurtboxDefinition hurtbox,
        CharacterVitality vitality,
        ActionSim actionSim,
        Func<SimActorId> simulationIdProvider,
        CharacterMotorSim motorSim,
        Func<SimActorId, NumericSystem> numericLookup = null,
        Func<bool> participationProvider = null)
    {
        _root = root;
        _aimTransform = aimTransform != null ? aimTransform : root;
        TeamId = teamId;
        _hurtbox = hurtbox ?? new HurtboxDefinition();
        _vitality = vitality ?? throw new ArgumentNullException(nameof(vitality));
        _actionSim = actionSim;
        _simulationIdProvider = simulationIdProvider;
        _motorSim = motorSim ?? throw new ArgumentNullException(nameof(motorSim));
        _numericLookup = numericLookup;
        _participationProvider = participationProvider;
    }

    /// <summary>Host 就绪后注入攻击者 Numeric 查找（敌人延迟装配）。</summary>
    public void SetNumericLookup(Func<SimActorId, NumericSystem> numericLookup) =>
        _numericLookup = numericLookup;

    /// <inheritdoc />
    public SimActorId SimulationId => _simulationIdProvider?.Invoke() ?? SimActorId.Invalid;

    /// <inheritdoc />
    public Transform TargetTransform => _root;

    /// <summary>索敌瞄准点。</summary>
    public Transform AimTransform => _aimTransform;

    /// <inheritdoc />
    public bool IsAlive =>
        _root != null
        && _root.gameObject.activeInHierarchy
        && (_participationProvider == null || _participationProvider())
        && !_vitality.IsDead;

    /// <inheritdoc />
    public float CurrentHealth => _vitality.CurrentHealth;

    /// <summary>角色阵营 id。</summary>
    public int TeamId { get; }

    /// <inheritdoc />
    public bool IsInvincible => QueryDefensiveWindow(perfectDodge: false);

    /// <inheritdoc />
    public bool IsInPerfectDodgeWindow => QueryDefensiveWindow(perfectDodge: true);

    /// <inheritdoc />
    public HitboxOrientedBox GetLogicalHurtbox()
    {
        SimCombatPose pose = GetLogicalCombatPose();
        return HitboxMath.BuildFromHurtboxLogical(in pose, _hurtbox);
    }

    /// <inheritdoc />
    public SimCombatPose GetLogicalCombatPose()
    {
        float heightY = _root != null ? _root.position.y : 0f;
        return SimCombatPose.FromMotor(_motorSim, heightY);
    }

    /// <summary>按攻防公式结算伤害并写入 Health Attribute。</summary>
    public void OnHit(in ActionHitContext context)
    {
        if (!IsAlive)
            return;

        NumericSystem attacker = _numericLookup?.Invoke(context.AttackerId);
        float damage = CombatDamageCalculator.Calculate(in context, attacker, _vitality.Numeric);
        _vitality.ApplyDamage(damage, in context);
    }

    bool QueryDefensiveWindow(bool perfectDodge)
    {
        if (_actionSim == null || !_actionSim.IsActive)
            return false;

        ActionSimSnapshot snap = _actionSim.Snapshot;
        if (snap.Content is not ActionDefinition action)
            return false;

        return perfectDodge
            ? action.IsPerfectDodgeWindowActiveAtFrame(snap.CurrentFrame)
            : action.IsInvincibleAtFrame(snap.CurrentFrame);
    }
}

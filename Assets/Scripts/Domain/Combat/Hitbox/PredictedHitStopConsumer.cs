using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 客机预测卡肉：几何重叠后只写 <see cref="ActionSim.RequestHitStop"/>。
/// 不进 Pipeline、不 OnHit、不写伤害。
/// </summary>
public sealed class PredictedHitStopConsumer : ICombatFrameConsumer
{
    readonly int _attackerTeamId;
    readonly ActionSim _actionSim;
    readonly Func<IReadOnlyList<IHurtboxTarget>> _activeTargetsProvider;
    readonly Func<SimActorId> _attackerIdProvider;
    readonly HitboxAttackBoxCache _boxes;
    readonly HashSet<(int HitboxIndex, SimActorId TargetId)> _hitPairs = new();
    int _trackedActionInstanceId;

    /// <summary>绑定与权威 Hitbox 相同的盒解析与花名册；禁止传入 CombatHitPipeline。</summary>
    public PredictedHitStopConsumer(
        Transform actorRoot,
        CharacterMotorSim motorSim,
        int teamId,
        ActionSim actionSim,
        CharacterAttachPointResolver attachPointResolver,
        Func<IReadOnlyList<IHurtboxTarget>> targetsProvider,
        Func<SimActorId> resolveAttackerId)
    {
        _attackerTeamId = teamId;
        _actionSim = actionSim;
        _activeTargetsProvider = targetsProvider;
        _attackerIdProvider = resolveAttackerId;
        _boxes = new HitboxAttackBoxCache(actorRoot, motorSim, attachPointResolver);
    }

    /// <inheritdoc />
    public void OnActionBegan(ActionDefinition action)
    {
        _trackedActionInstanceId = 0;
        _hitPairs.Clear();
        _boxes.Clear();
    }

    /// <inheritdoc />
    public void OnCombatFrameAdvanced(in CombatFrameContext context)
    {
        if (context.Action == null)
            return;

        if (_trackedActionInstanceId != context.ActionInstanceId)
        {
            _trackedActionInstanceId = context.ActionInstanceId;
            _hitPairs.Clear();
            _boxes.Clear();
        }

        _boxes.Prune(context.Action, context.FrameIndex);
        HitDetector.ApplyPredictedHitStopAtFrame(
            context.Action,
            context.FrameIndex,
            _attackerTeamId,
            _boxes.Resolve,
            _hitPairs,
            _actionSim,
            _activeTargetsProvider?.Invoke(),
            _attackerIdProvider?.Invoke() ?? SimActorId.Invalid,
            context.ActionInstanceId);
    }

    /// <inheritdoc />
    public void OnActionEnded()
    {
        _trackedActionInstanceId = 0;
        _hitPairs.Clear();
        _boxes.Clear();
    }
}

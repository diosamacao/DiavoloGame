using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>统一收集、稳定排序并在全部 Actor Step 后结算当前逻辑帧命中。</summary>
public sealed class CombatHitPipeline
{
    readonly List<CombatHitEvent> _pending = new();
    readonly List<ResolvedCombatHit> _resolved = new();
    readonly Action<ResolvedCombatHit> _publishResolvedHit;
    Func<SimActorId, NumericSystem> _numericLookup;
    long _collectingFrame = -1;

    /// <summary>创建帧末命中流水线；发布回调只能消费只读表现结果。</summary>
    public CombatHitPipeline(Action<ResolvedCombatHit> publishResolvedHit)
    {
        _publishResolvedHit = publishResolvedHit;
    }

    /// <summary>绑定攻击者/防御者 Numeric 查找，供 Grant 与完美闪避武装。</summary>
    public void BindNumericLookup(Func<SimActorId, NumericSystem> lookup) =>
        _numericLookup = lookup;

    /// <summary>开始收集下一逻辑帧；遗留事件会被清除，避免异常帧污染后续结算。</summary>
    public void BeginFrame(long frame)
    {
        if (frame < 0)
            throw new ArgumentOutOfRangeException(nameof(frame));

        _collectingFrame = frame;
        _pending.Clear();
        _resolved.Clear();
    }

    /// <summary>记录几何检测通过的命中；此阶段不得修改目标 HP、状态或攻击者会话。</summary>
    public void Collect(
        SimActorId attackerId,
        int actionInstanceId,
        int hitboxIndex,
        IHurtboxTarget target,
        IActionSimHitReceiver hitReceiver,
        in ActionHitContext context,
        Vector3 hitPoint)
    {
        if (_collectingFrame < 0
            || !attackerId.IsValid
            || target == null
            || !target.SimulationId.IsValid)
        {
            return;
        }

        var key = new SimHitKey(
            _collectingFrame,
            attackerId,
            actionInstanceId,
            hitboxIndex,
            target.SimulationId);
        _pending.Add(new CombatHitEvent(
            key,
            context,
            target,
            hitReceiver,
            target.TargetTransform,
            hitPoint));
    }

    /// <summary>先结算 Actor Step 收集的命中，使 PostCombat 可读取同帧命中确认。</summary>
    public void ResolveBeforePostCombat(long frame)
    {
        ValidateFrame(frame);
        ResolvePending();
    }

    /// <summary>结算 PostCombat 转场在 frame 0 新增的命中，随后一次性发布整帧表现结果。</summary>
    public void CompleteFrame(long frame)
    {
        ValidateFrame(frame);
        ResolvePending();

        for (int i = 0; i < _resolved.Count; i++)
            _publishResolvedHit?.Invoke(_resolved[i]);

        _resolved.Clear();
        _collectingFrame = -1;
    }

    /// <summary>对当前批次按稳定键排序并应用权威伤害、Reaction 与命中确认。</summary>
    void ResolvePending()
    {
        _pending.Sort(CompareEvents);
        for (int i = 0; i < _pending.Count; i++)
        {
            CombatHitEvent hit = _pending[i];
            if (hit.Target == null
                || (hit.Target is ITargetable targetable && !targetable.IsAlive))
            {
                continue;
            }

            ActionHitContext context = hit.Context;

            // 完美闪避优先于普通无敌：吞伤、不 Grant、武装反击缓冲
            if (hit.Target is IHitAbsorbQuery absorb && absorb.IsInPerfectDodgeWindow)
            {
                _numericLookup?.Invoke(hit.Key.TargetId)?.ArmPerfectDodgeCounter();
                hit.HitReceiver?.ConfirmHit(hit.Key.ActionInstanceId);
                // 吞伤仍发布事件（相机等可订阅），但标记 PD 供受击 Cue 跳过
                _resolved.Add(new ResolvedCombatHit(
                    context,
                    hit.TargetTransform,
                    ResolveHitDirection(hit.Context.Attacker, hit.TargetTransform),
                    hit.HitPoint,
                    absorbedByPerfectDodge: true));
                continue;
            }

            if (hit.Target is IHitAbsorbQuery invuln && invuln.IsInvincible)
            {
                hit.HitReceiver?.ConfirmHit(hit.Key.ActionInstanceId);
                continue;
            }

            hit.Target.OnHit(in context);
            hit.HitReceiver?.ConfirmHit(hit.Key.ActionInstanceId);

            NumericSystem attackerNumeric = _numericLookup?.Invoke(hit.Key.AttackerId);
            if (attackerNumeric != null)
            {
                ActionResourceSpec spec = context.Action != null
                    ? context.Action.ResourceSpec
                    : ActionResourceSpec.Empty;
                ActionResourceSpecEffectCompiler.ApplyGrant(attackerNumeric, spec);
            }

            HitFeedbackSettings feedback = context.Hitbox != null
                ? context.Hitbox.Payload.Feedback
                : null;
            if (feedback != null && feedback.UseHitStop && hit.HitReceiver != null)
            {
                hit.HitReceiver.RequestHitStop(
                    hit.Key.ActionInstanceId,
                    feedback.HitStopFrames,
                    feedback.HitStopOncePerAction);
            }

            _resolved.Add(new ResolvedCombatHit(
                context,
                hit.TargetTransform,
                ResolveHitDirection(hit.Context.Attacker, hit.TargetTransform),
                hit.HitPoint));
        }

        _pending.Clear();
    }

    void ValidateFrame(long frame)
    {
        if (frame != _collectingFrame)
        {
            throw new InvalidOperationException(
                $"CombatHitPipeline 结算帧 {frame} 与收集帧 {_collectingFrame} 不一致。");
        }
    }

    static int CompareEvents(CombatHitEvent left, CombatHitEvent right) =>
        left.Key.CompareTo(right.Key);

    static Vector3 ResolveHitDirection(Transform attacker, Transform target)
    {
        if (attacker == null)
            return Vector3.forward;

        if (target != null)
        {
            Vector3 toTarget = target.position - attacker.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
                return toTarget.normalized;
        }

        Vector3 forward = attacker.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>统一收集、稳定排序并在全部 Actor Step 后结算当前逻辑帧命中。</summary>
public sealed class CombatHitPipeline
{
    readonly List<CombatHitEvent> _pending = new();
    readonly List<ResolvedCombatHit> _resolved = new();
    readonly Action<ResolvedCombatHit> _publishResolvedHit;
    Func<SimActorId, CharacterResourceSim> _resourceLookup;
    long _collectingFrame = -1;

    /// <summary>创建帧末命中流水线；发布回调只能消费只读表现结果。</summary>
    public CombatHitPipeline(Action<ResolvedCombatHit> publishResolvedHit)
    {
        _publishResolvedHit = publishResolvedHit;
    }

    /// <summary>绑定攻击者资源查找，供 ConfirmHit 后 GrantOnHit。</summary>
    public void BindResourceLookup(Func<SimActorId, CharacterResourceSim> lookup) =>
        _resourceLookup = lookup;

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
        in ActionHitContext context)
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
            target.TargetTransform));
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

        // App 反馈晚于整批 Sim 结算，事件订阅者不能影响本帧后续命中的权威结果。
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

            // 全部检测已结束后才按稳定键写入权威状态，Actor 注册顺序不再改变检测阶段的目标状态。
            ActionHitContext context = hit.Context;
            hit.Target.OnHit(in context);
            hit.HitReceiver?.ConfirmHit(hit.Key.ActionInstanceId);

            // 有效几何命中确认后回填资源；Collect 阶段禁止副作用
            CharacterResourceSim attackerResources = _resourceLookup?.Invoke(hit.Key.AttackerId);
            attackerResources?.GrantOnHit(context.Action != null
                ? context.Action.ResourceSpec
                : ActionResourceSpec.Empty);

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
                ResolveHitDirection(hit.Context.Attacker, hit.TargetTransform)));
        }

        _pending.Clear();
    }

    /// <summary>拒绝跨帧或未 BeginFrame 的错误结算调用。</summary>
    void ValidateFrame(long frame)
    {
        if (frame != _collectingFrame)
        {
            throw new InvalidOperationException(
                $"CombatHitPipeline 结算帧 {frame} 与收集帧 {_collectingFrame} 不一致。");
        }
    }

    /// <summary>按纯模拟键比较命中，不读取 Transform 或 Unity 实例身份。</summary>
    static int CompareEvents(CombatHitEvent left, CombatHitEvent right) =>
        left.Key.CompareTo(right.Key);

    /// <summary>计算帧末表现反馈使用的水平命中方向。</summary>
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

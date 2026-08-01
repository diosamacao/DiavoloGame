using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>攻击侧 Hitbox 帧消费者：订阅表现桥帧派发并向共享流水线收集命中。</summary>
public sealed class HitboxFrameConsumer : ICombatFrameConsumer
{
    readonly Transform root;
    readonly int attackerTeamId;
    readonly CharacterAttachPointResolver attachPoints;
    readonly ActionSim _actionSim;
    readonly Func<IReadOnlyList<IHurtboxTarget>> activeTargetsProvider;
    readonly Func<SimActorId> attackerIdProvider;
    readonly CombatHitPipeline hitPipeline;

    readonly HashSet<(int HitboxIndex, SimActorId TargetId)> _hitPairs = new();
    int _trackedActionInstanceId;

    /// <summary>默认挂点；为空时使用角色根。</summary>
    public Transform AttachPoint => attachPoints != null ? attachPoints.DefaultAttach : root;

    /// <summary>创建纯 C# Hitbox 帧消费者；按 Hitbox.attachPointId 解析挂点。</summary>
    public HitboxFrameConsumer(
        Transform actorRoot,
        int teamId,
        ActionSim actionSim,
        CharacterAttachPointResolver attachPointResolver,
        Func<IReadOnlyList<IHurtboxTarget>> targetsProvider,
        Func<SimActorId> resolveAttackerId,
        CombatHitPipeline combatHitPipeline)
    {
        root = actorRoot;
        attackerTeamId = teamId;
        _actionSim = actionSim;
        attachPoints = attachPointResolver;
        activeTargetsProvider = targetsProvider;
        attackerIdProvider = resolveAttackerId;
        hitPipeline = combatHitPipeline;
    }

    /// <summary>新招式开始：清空命中缓存。</summary>
    public void OnActionBegan(ActionDefinition action)
    {
        _trackedActionInstanceId = 0;
        _hitPairs.Clear();
    }

    /// <summary>Logic Tick 帧推进：每个 Hitbox 窗口对每个目标最多结算一次。</summary>
    public void OnCombatFrameAdvanced(in CombatFrameContext context)
    {
        if (context.Action == null)
            return;

        ClearHitCacheIfNeeded(context.ActionInstanceId);
        ProcessHitboxesAtFrame(context.Action, context.FrameIndex, context.ActionInstanceId);
    }

    /// <summary>招式结束：清空追踪状态。</summary>
    public void OnActionEnded()
    {
        _trackedActionInstanceId = 0;
        _hitPairs.Clear();
    }

    void ProcessHitboxesAtFrame(ActionDefinition action, int frame, int actionInstanceId)
    {
        IReadOnlyList<IHurtboxTarget> activeTargets = activeTargetsProvider?.Invoke();
        HitDetector.ProcessHitboxesAtFrame(
            action,
            frame,
            root,
            attackerTeamId,
            ResolveHitboxAnchor,
            _hitPairs,
            _actionSim,
            activeTargets,
            attackerIdProvider?.Invoke() ?? SimActorId.Invalid,
            actionInstanceId,
            hitPipeline);
    }

    /// <summary>按 Hitbox 自身 attachPointId 解析世界挂点。</summary>
    Transform ResolveHitboxAnchor(HitboxNotifyState hitbox)
    {
        if (attachPoints == null)
            return root;

        return attachPoints.Resolve(hitbox != null ? hitbox.AttachPointId : null);
    }

    /// <summary>切换稳定动作实例时清空命中缓存，允许同一内容连续播放。</summary>
    void ClearHitCacheIfNeeded(int actionInstanceId)
    {
        if (_trackedActionInstanceId == actionInstanceId)
            return;

        _trackedActionInstanceId = actionInstanceId;
        _hitPairs.Clear();
    }

    /// <summary>绘制指定招式在某帧的全部生效 Hitbox（Play Mode Gizmo）。</summary>
    public void DrawActionHitboxes(ActionDefinition action, int frame, bool editorPreview, int selectedIndex)
    {
        if (action == null)
            return;

        HitboxNotifyState[] allHitboxes = action.HitboxStates;

        for (int i = 0; i < allHitboxes.Length; i++)
        {
            HitboxNotifyState hitbox = allHitboxes[i];
            if (hitbox == null)
                continue;

            bool isActive = hitbox.IsActiveAtFrame(frame);
            bool isSelected = i == selectedIndex;
            Color color = isSelected
                ? new Color(1f, 0.85f, 0.1f, 1f)
                : isActive
                    ? new Color(1f, 0.35f, 0.15f, 0.95f)
                    : new Color(0.6f, 0.6f, 0.6f, 0.35f);

            if (!editorPreview && !isActive)
                continue;

            Transform anchor = ResolveHitboxAnchor(hitbox);
            HitboxOrientedBox box = HitboxMath.BuildFromHitbox(root, anchor, hitbox);
            HitboxGizmoDrawing.DrawWireOrientedBox(box, color);
        }
    }
}

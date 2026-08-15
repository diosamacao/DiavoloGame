using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 攻击侧 Hitbox 帧消费者：按 MotorSim 逻辑根收集命中。
/// parentToAttachPoint=false 时在窗口进入帧冻结世界 OBB，后续帧不再跟随根移动。
/// </summary>
public sealed class HitboxFrameConsumer : ICombatFrameConsumer
{
    readonly Transform root;
    readonly int attackerTeamId;
    readonly ActionSim _actionSim;
    readonly Func<IReadOnlyList<IHurtboxTarget>> activeTargetsProvider;
    readonly Func<SimActorId> attackerIdProvider;
    readonly CombatHitPipeline hitPipeline;
    readonly HitboxAttackBoxCache _boxes;

    readonly HashSet<(int HitboxIndex, SimActorId TargetId)> _hitPairs = new();
    int _trackedActionInstanceId;

    /// <summary>默认挂点；为空时使用角色根。</summary>
    public Transform AttachPoint { get; }

    /// <summary>创建 Hitbox 帧消费者；水平根权威来自 MotorSim。</summary>
    public HitboxFrameConsumer(
        Transform actorRoot,
        CharacterMotorSim motorSim,
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
        activeTargetsProvider = targetsProvider;
        attackerIdProvider = resolveAttackerId;
        hitPipeline = combatHitPipeline;
        _boxes = new HitboxAttackBoxCache(actorRoot, motorSim, attachPointResolver);
        AttachPoint = attachPointResolver != null ? attachPointResolver.DefaultAttach : actorRoot;
    }

    /// <summary>新招式开始：清空命中缓存与世界空间冻结盒。</summary>
    public void OnActionBegan(ActionDefinition action)
    {
        _trackedActionInstanceId = 0;
        _hitPairs.Clear();
        _boxes.Clear();
    }

    /// <summary>Logic Tick 帧推进：每个 Hitbox 窗口对每个目标最多结算一次。</summary>
    public void OnCombatFrameAdvanced(in CombatFrameContext context)
    {
        if (context.Action == null)
            return;

        ClearHitCacheIfNeeded(context.ActionInstanceId);
        ProcessHitboxesAtFrame(context.Action, context.FrameIndex, context.ActionInstanceId);
    }

    /// <summary>招式结束：清空追踪状态与冻结盒。</summary>
    public void OnActionEnded()
    {
        _trackedActionInstanceId = 0;
        _hitPairs.Clear();
        _boxes.Clear();
    }

    void ProcessHitboxesAtFrame(ActionDefinition action, int frame, int actionInstanceId)
    {
        _boxes.Prune(action, frame);

        IReadOnlyList<IHurtboxTarget> activeTargets = activeTargetsProvider?.Invoke();

        HitDetector.ProcessHitboxesAtFrame(
            action,
            frame,
            attackerTeamId,
            _boxes.Resolve,
            _hitPairs,
            _actionSim,
            activeTargets,
            attackerIdProvider?.Invoke() ?? SimActorId.Invalid,
            actionInstanceId,
            hitPipeline,
            root);
    }

    /// <summary>切换稳定动作实例时清空命中缓存，允许同一内容连续播放。</summary>
    void ClearHitCacheIfNeeded(int actionInstanceId)
    {
        if (_trackedActionInstanceId == actionInstanceId)
            return;

        _trackedActionInstanceId = actionInstanceId;
        _hitPairs.Clear();
        _boxes.Clear();
    }

    /// <summary>绘制指定招式在某帧的全部生效 Hitbox（Play Mode Gizmo）。</summary>
    public void DrawActionHitboxes(ActionDefinition action, int frame, bool editorPreview, int selectedIndex)
    {
        if (action == null)
            return;

        HitboxNotifyState[] allHitboxes = action.HitboxStates;
        _boxes.Prune(action, frame);

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

            // 编辑器预览未进窗口时仍画跟随盒，便于摆位置；世界空间仅在激活后冻结
            HitboxOrientedBox box = isActive || hitbox.ParentToAttachPoint
                ? _boxes.Resolve(i, hitbox)
                : _boxes.BuildFollowAttachBox(hitbox);
            HitboxGizmoDrawing.DrawWireOrientedBox(box, color);
        }
    }
}

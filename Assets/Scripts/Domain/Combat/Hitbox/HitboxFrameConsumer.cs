using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>攻击侧 Hitbox 帧消费者：订阅 ActionExecutor Logic Tick 并检测当前帧命中。</summary>
public sealed class HitboxFrameConsumer : ICombatFrameConsumer
{
    readonly Transform root;
    readonly int attackerTeamId;
    readonly CharacterAttachPointResolver attachPoints;
    readonly ActionExecutor actionExecutor;
    readonly Func<IReadOnlyList<IHurtboxTarget>> activeTargetsProvider;
    readonly Action<ActionHitContext, IHurtboxTarget, IActionHitReceiver, Transform> hitDetected;

    readonly HashSet<(int HitboxIndex, int TargetId)> _hitPairs = new();
    ActionDefinition _trackedAction;

    /// <summary>默认挂点；为空时使用角色根。</summary>
    public Transform AttachPoint => attachPoints != null ? attachPoints.DefaultAttach : root;

    /// <summary>招式运行时只读访问，供帧采样与 Hitbox 检测。</summary>
    IActionExecutor Runtime => actionExecutor;

    /// <summary>创建纯 C# Hitbox 帧消费者；按 Hitbox.attachPointId 解析挂点。</summary>
    public HitboxFrameConsumer(
        Transform actorRoot,
        int teamId,
        ActionExecutor executor,
        CharacterAttachPointResolver attachPointResolver,
        Func<IReadOnlyList<IHurtboxTarget>> targetsProvider,
        Action<ActionHitContext, IHurtboxTarget, IActionHitReceiver, Transform> onHitDetected)
    {
        root = actorRoot;
        attackerTeamId = teamId;
        actionExecutor = executor;
        attachPoints = attachPointResolver;
        activeTargetsProvider = targetsProvider;
        hitDetected = onHitDetected;
    }

    /// <summary>新招式开始：清空命中缓存。</summary>
    public void OnActionBegan(ActionDefinition action)
    {
        ClearHitCacheIfNeeded(action);
    }

    /// <summary>Logic Tick 帧推进：每个 Hitbox 窗口对每个目标最多结算一次。</summary>
    public void OnCombatFrameAdvanced(in CombatFrameContext context)
    {
        if (context.Action == null)
            return;

        ClearHitCacheIfNeeded(context.Action);
        ProcessHitboxesAtFrame(context.Action, context.FrameIndex);
    }

    /// <summary>招式结束：清空追踪状态。</summary>
    public void OnActionEnded()
    {
        ClearHitCacheIfNeeded(null);
    }

    void ProcessHitboxesAtFrame(ActionDefinition action, int frame)
    {
        IReadOnlyList<IHurtboxTarget> activeTargets = activeTargetsProvider?.Invoke();
        HitDetector.ProcessHitboxesAtFrame(
            action,
            frame,
            root,
            attackerTeamId,
            ResolveHitboxAnchor,
            _hitPairs,
            actionExecutor,
            activeTargets,
            hitDetected);
    }

    /// <summary>按 Hitbox 自身 attachPointId 解析世界挂点。</summary>
    Transform ResolveHitboxAnchor(HitboxNotifyState hitbox)
    {
        if (attachPoints == null)
            return root;

        return attachPoints.Resolve(hitbox != null ? hitbox.AttachPointId : null);
    }

    /// <summary>切换招式时清空命中缓存，避免跨招误判。</summary>
    void ClearHitCacheIfNeeded(ActionDefinition action)
    {
        if (_trackedAction == action)
            return;

        _trackedAction = action;
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

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>攻击侧 Hitbox 帧消费者：按 MotorSim 逻辑根收集命中。</summary>
public sealed class HitboxFrameConsumer : ICombatFrameConsumer
{
    readonly Transform root;
    readonly CharacterMotorSim _motorSim;
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
        _motorSim = motorSim ?? throw new ArgumentNullException(nameof(motorSim));
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
        float heightY = root != null ? root.position.y : 0f;
        SimCombatPose attackerPose = SimCombatPose.FromMotor(_motorSim, heightY);

        HitDetector.ProcessHitboxesAtFrame(
            action,
            frame,
            attackerPose,
            attackerTeamId,
            ResolveAttachLocalPosition,
            ResolveAttachLocalRotation,
            _hitPairs,
            _actionSim,
            activeTargets,
            attackerIdProvider?.Invoke() ?? SimActorId.Invalid,
            actionInstanceId,
            hitPipeline,
            root);
    }

    /// <summary>挂点相对角色根的局部位置；供逻辑根合成世界盒。</summary>
    Vector3 ResolveAttachLocalPosition(HitboxNotifyState hitbox)
    {
        Transform anchor = ResolveHitboxAnchor(hitbox);
        if (root == null || anchor == null || anchor == root)
            return Vector3.zero;

        return root.InverseTransformPoint(anchor.position);
    }

    /// <summary>挂点相对角色根的局部旋转。</summary>
    Quaternion ResolveAttachLocalRotation(HitboxNotifyState hitbox)
    {
        Transform anchor = ResolveHitboxAnchor(hitbox);
        if (root == null || anchor == null || anchor == root)
            return Quaternion.identity;

        return Quaternion.Inverse(root.rotation) * anchor.rotation;
    }

    /// <summary>按 Hitbox 自身 attachPointId 解析模型挂点（仅取相对根局部）。</summary>
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
        float heightY = root != null ? root.position.y : 0f;
        SimCombatPose pose = SimCombatPose.FromMotor(_motorSim, heightY);

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

            HitboxOrientedBox box = HitboxMath.BuildFromHitboxLogical(
                in pose,
                ResolveAttachLocalPosition(hitbox),
                ResolveAttachLocalRotation(hitbox),
                hitbox);
            HitboxGizmoDrawing.DrawWireOrientedBox(box, color);
        }
    }
}

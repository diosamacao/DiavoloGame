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
    readonly CharacterMotorSim _motorSim;
    readonly int attackerTeamId;
    readonly CharacterAttachPointResolver attachPoints;
    readonly ActionSim _actionSim;
    readonly Func<IReadOnlyList<IHurtboxTarget>> activeTargetsProvider;
    readonly Func<SimActorId> attackerIdProvider;
    readonly CombatHitPipeline hitPipeline;

    readonly HashSet<(int HitboxIndex, SimActorId TargetId)> _hitPairs = new();
    /// <summary>世界空间 Hitbox：按 hitboxIndex 缓存进入窗口时的 OBB。</summary>
    readonly Dictionary<int, HitboxOrientedBox> _frozenWorldBoxes = new();
    readonly List<int> _staleFrozenKeys = new();
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

    /// <summary>新招式开始：清空命中缓存与世界空间冻结盒。</summary>
    public void OnActionBegan(ActionDefinition action)
    {
        _trackedActionInstanceId = 0;
        _hitPairs.Clear();
        _frozenWorldBoxes.Clear();
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
        _frozenWorldBoxes.Clear();
    }

    void ProcessHitboxesAtFrame(ActionDefinition action, int frame, int actionInstanceId)
    {
        PruneFrozenBoxes(action, frame);

        IReadOnlyList<IHurtboxTarget> activeTargets = activeTargetsProvider?.Invoke();

        HitDetector.ProcessHitboxesAtFrame(
            action,
            frame,
            attackerTeamId,
            ResolveAttackBox,
            _hitPairs,
            _actionSim,
            activeTargets,
            attackerIdProvider?.Invoke() ?? SimActorId.Invalid,
            actionInstanceId,
            hitPipeline,
            root);
    }

    /// <summary>
    /// 解析攻击盒：跟随挂点则每帧重建；世界空间则进入窗口时冻结。
    /// </summary>
    HitboxOrientedBox ResolveAttackBox(int hitboxIndex, HitboxNotifyState hitbox)
    {
        if (hitbox.ParentToAttachPoint)
        {
            _frozenWorldBoxes.Remove(hitboxIndex);
            return BuildFollowAttachBox(hitbox);
        }

        if (_frozenWorldBoxes.TryGetValue(hitboxIndex, out HitboxOrientedBox frozen))
            return frozen;

        HitboxOrientedBox captured = BuildFollowAttachBox(hitbox);
        _frozenWorldBoxes[hitboxIndex] = captured;
        return captured;
    }

    /// <summary>按当前逻辑根 + 挂点局部 TRS 构建跟随盒。</summary>
    HitboxOrientedBox BuildFollowAttachBox(HitboxNotifyState hitbox)
    {
        float heightY = root != null ? root.position.y : 0f;
        SimCombatPose attackerPose = SimCombatPose.FromMotor(_motorSim, heightY);
        return HitboxMath.BuildFromHitboxLogical(
            in attackerPose,
            ResolveAttachLocalPosition(hitbox),
            ResolveAttachLocalRotation(hitbox),
            hitbox);
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

    /// <summary>窗口已退出的世界空间盒丢弃，下次进入重新捕获。</summary>
    void PruneFrozenBoxes(ActionDefinition action, int frame)
    {
        if (_frozenWorldBoxes.Count == 0)
            return;

        HitboxNotifyState[] hitboxes = action.HitboxStates;
        _staleFrozenKeys.Clear();
        foreach (KeyValuePair<int, HitboxOrientedBox> pair in _frozenWorldBoxes)
        {
            int index = pair.Key;
            if (hitboxes == null
                || index < 0
                || index >= hitboxes.Length
                || hitboxes[index] == null
                || !hitboxes[index].IsActiveAtFrame(frame)
                || hitboxes[index].ParentToAttachPoint)
            {
                _staleFrozenKeys.Add(index);
            }
        }

        for (int i = 0; i < _staleFrozenKeys.Count; i++)
            _frozenWorldBoxes.Remove(_staleFrozenKeys[i]);
    }

    /// <summary>切换稳定动作实例时清空命中缓存，允许同一内容连续播放。</summary>
    void ClearHitCacheIfNeeded(int actionInstanceId)
    {
        if (_trackedActionInstanceId == actionInstanceId)
            return;

        _trackedActionInstanceId = actionInstanceId;
        _hitPairs.Clear();
        _frozenWorldBoxes.Clear();
    }

    /// <summary>绘制指定招式在某帧的全部生效 Hitbox（Play Mode Gizmo）。</summary>
    public void DrawActionHitboxes(ActionDefinition action, int frame, bool editorPreview, int selectedIndex)
    {
        if (action == null)
            return;

        HitboxNotifyState[] allHitboxes = action.HitboxStates;
        PruneFrozenBoxes(action, frame);

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
                ? ResolveAttackBox(i, hitbox)
                : BuildFollowAttachBox(hitbox);
            HitboxGizmoDrawing.DrawWireOrientedBox(box, color);
        }
    }
}

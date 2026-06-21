using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 攻击侧 Hitbox 运行时：订阅 ActionRuntimeController Logic Tick，
/// 与场景 IHurtboxTarget 做 OBB 重叠检测。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ActionRuntimeController))]
public class HitBoxSystem : MonoBehaviour, ICombatFrameConsumer
{
    [SerializeField] ActionRuntimeController actionRuntime = null!;
    [Tooltip("Hitbox 局部变换的挂点；为空时使用本物体 Transform。")]
    [SerializeField] Transform attachPoint = null;

    /// <summary>Hitbox 局部变换挂点；为空时使用本物体 Transform。</summary>
    public Transform AttachPoint => attachPoint;

    readonly HashSet<(string HitboxId, int TargetId)> _hitPairs = new();
    ActionDefinition _trackedAction;

    /// <summary>招式运行时只读访问，供帧采样与 Hitbox 检测。</summary>
    IActionRuntime Runtime => actionRuntime;

    void Awake()
    {
        if (actionRuntime == null)
            actionRuntime = GetComponent<ActionRuntimeController>();
    }

    /// <summary>绑定运行时与默认挂点，供 CharacterConfig 统一装配。</summary>
    public void Bind(ActionRuntimeController runtime, Transform hitboxAttachPoint)
    {
        actionRuntime = runtime;
        attachPoint = hitboxAttachPoint;
    }

    /// <summary>新招式开始：清空命中缓存。</summary>
    public void OnActionBegan(ActionDefinition action)
    {
        ClearHitCacheIfNeeded(action);
    }

    /// <summary>Logic Tick 帧推进：检测当前帧生效的 Hitbox。</summary>
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
        Transform root = transform;
        Transform anchor = attachPoint != null ? attachPoint : root;
        HitDetectionSystem.ProcessHitboxesAtFrame(
            action,
            frame,
            root,
            anchor,
            _hitPairs,
            actionRuntime);
    }

    /// <summary>切换招式时清空命中缓存，避免跨招误判。</summary>
    void ClearHitCacheIfNeeded(ActionDefinition action)
    {
        if (_trackedAction == action)
            return;

        _trackedAction = action;
        _hitPairs.Clear();
    }

    void OnDrawGizmos()
    {
        if (actionRuntime == null || !Runtime.IsPlaying || Runtime.CurrentAction == null)
            return;

        DrawActionHitboxes(
            Runtime.CurrentAction,
            Runtime.CurrentFrame,
            false,
            -1);
    }

    /// <summary>绘制指定招式在某帧的全部生效 Hitbox（Play Mode Gizmo）。</summary>
    public void DrawActionHitboxes(ActionDefinition action, int frame, bool editorPreview, int selectedIndex)
    {
        if (action == null)
            return;

        Transform root = transform;
        Transform anchor = attachPoint != null ? attachPoint : root;
        HitboxKeyframe[] allHitboxes = action.Hitboxes;

        for (int i = 0; i < allHitboxes.Length; i++)
        {
            HitboxKeyframe hitbox = allHitboxes[i];
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

            HitboxOrientedBox box = HitboxMath.BuildFromHitbox(root, anchor, hitbox);
            HitboxGizmoDrawing.DrawWireOrientedBox(box, color);
        }
    }
}

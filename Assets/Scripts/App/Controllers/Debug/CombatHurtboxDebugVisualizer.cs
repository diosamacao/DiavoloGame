using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Play 模式逻辑 Hurtbox 线框；读取 <see cref="CombatHurtboxDebugSettings"/> 与 TargetSystem。
/// 与命中权威同构（GetLogicalHurtbox），不画 Physics Collider。
/// </summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
[DisallowMultipleComponent]
[DefaultExecutionOrder(1001)]
public sealed class CombatHurtboxDebugVisualizer : AppControllerBase
{
    static readonly Color PlayerColor = new(0.25f, 1f, 0.45f, 0.9f);
    static readonly Color EnemyColor = new(1f, 0.35f, 0.25f, 0.9f);
    static readonly Color NeutralColor = new(0.3f, 0.75f, 1f, 0.9f);

    void LateUpdate()
    {
        if (!CombatHurtboxDebugSettings.ShowHurtboxes)
            return;

        TargetSystem targets = GetSystem<TargetSystem>();
        if (targets == null)
            return;

        // 与命中同构：画逻辑 OBB，不画 Physics Collider
        IReadOnlyList<IHurtboxTarget> active = targets.ActiveTargets;
        for (int i = 0; i < active.Count; i++)
        {
            IHurtboxTarget target = active[i];
            if (!ShouldDraw(target))
                continue;

            DrawWireObb(target.GetLogicalHurtbox(), ResolveColor(target));
        }
    }

    void OnDrawGizmos()
    {
        // Scene 视图补绘；Game 视图主要靠 LateUpdate + Debug.DrawLine
        if (!CombatHurtboxDebugSettings.ShowHurtboxes)
            return;

        TargetSystem targets = GetSystem<TargetSystem>();
        if (targets == null)
            return;

        IReadOnlyList<IHurtboxTarget> active = targets.ActiveTargets;
        for (int i = 0; i < active.Count; i++)
        {
            IHurtboxTarget target = active[i];
            if (!ShouldDraw(target))
                continue;

            HitboxGizmoDrawing.DrawWireOrientedBox(target.GetLogicalHurtbox(), ResolveColor(target));
        }
    }

    /// <summary>IsAlive 在 ITargetable 上；非 Targetable 仍绘制。</summary>
    static bool ShouldDraw(IHurtboxTarget target)
    {
        if (target == null)
            return false;
        if (target is ITargetable targetable && !targetable.IsAlive)
            return false;
        return true;
    }

    /// <summary>按阵营区分线框色：TeamId 0 绿，其它红。</summary>
    static Color ResolveColor(IHurtboxTarget target)
    {
        if (target is ITargetable targetable)
            return targetable.TeamId == 0 ? PlayerColor : EnemyColor;

        return NeutralColor;
    }

    /// <summary>用 Debug.DrawLine 画 OBB 十二棱，Game 视图无需开 Gizmos 按钮即可见。</summary>
    static void DrawWireObb(HitboxOrientedBox box, Color color)
    {
        Vector3 c = box.Center;
        Vector3 x = box.GetAxis(0) * box.HalfExtents.x;
        Vector3 y = box.GetAxis(1) * box.HalfExtents.y;
        Vector3 z = box.GetAxis(2) * box.HalfExtents.z;

        Vector3 p000 = c - x - y - z;
        Vector3 p001 = c - x - y + z;
        Vector3 p010 = c - x + y - z;
        Vector3 p011 = c - x + y + z;
        Vector3 p100 = c + x - y - z;
        Vector3 p101 = c + x - y + z;
        Vector3 p110 = c + x + y - z;
        Vector3 p111 = c + x + y + z;

        DrawEdge(p000, p001, color);
        DrawEdge(p001, p011, color);
        DrawEdge(p011, p010, color);
        DrawEdge(p010, p000, color);
        DrawEdge(p100, p101, color);
        DrawEdge(p101, p111, color);
        DrawEdge(p111, p110, color);
        DrawEdge(p110, p100, color);
        DrawEdge(p000, p100, color);
        DrawEdge(p001, p101, color);
        DrawEdge(p010, p110, color);
        DrawEdge(p011, p111, color);
    }

    static void DrawEdge(Vector3 a, Vector3 b, Color color) =>
        Debug.DrawLine(a, b, color, 0f, depthTest: false);
}
#endif

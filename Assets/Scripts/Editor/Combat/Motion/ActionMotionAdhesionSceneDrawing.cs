using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Action Editor Scene：假敌球 + TargetAdhesion 修正轨迹预览（对齐运行时 Base→Modifier 顺序）。
/// </summary>
public static class ActionMotionAdhesionSceneDrawing
{
    static readonly Color EnemyColor = new(1f, 0.35f, 0.3f, 0.95f);
    static readonly Color DesiredColor = new(1f, 0.9f, 0.25f, 0.95f);
    static readonly Color AdhesionPathColor = new(0.35f, 1f, 0.45f, 0.95f);
    static readonly Color BakedOnlyColor = new(0.2f, 0.85f, 1f, 0.55f);
    static readonly List<Vector3> s_path = new(128);

    /// <summary>假敌球显示半径（米）。</summary>
    public const float EnemySphereRadius = 0.22f;

    /// <summary>从 Timeline 窗口构造 Simulation 纯参。</summary>
    public static ActionMotionAdhesionParams ToParams(MotionModifierNotifyState window)
    {
        if (window == null)
            return default;

        return new ActionMotionAdhesionParams(
            window.StartFrame,
            window.EndFrame,
            window.HorizontalOffsetMm,
            window.LateralOffsetMm,
            window.MaxCorrectionMmPerFrame,
            window.MaxAcquireDistanceMm,
            window.MaxAngleMilliDeg);
    }

    /// <summary>
    /// 绘制假敌与（TargetAdhesion 时）吸附路径；返回当前帧吸附后角色水平落点。
    /// </summary>
    public static void Draw(
        ActionDefinition action,
        MotionModifierNotifyState window,
        Vector3 originPosition,
        Quaternion originRotation,
        Vector3 enemyWorld,
        int previewFrame,
        out Vector3 adhesionActorWorld)
    {
        adhesionActorWorld = originPosition;
        if (window == null)
            return;

        DrawEnemyMarker(enemyWorld, window.Mode);

        if (window.Mode != MotionModifierMode.TargetAdhesion)
            return;

        ActionMotionAdhesionParams adhesion = ToParams(window);
        float yaw = originRotation.eulerAngles.y;
        SimulateThroughFrame(
            action,
            in adhesion,
            originPosition,
            originRotation,
            enemyWorld,
            yaw,
            previewFrame,
            s_path,
            out adhesionActorWorld,
            out Vector3 desiredWorld,
            out Vector3 bakedOnlyWorld);

        if (s_path.Count >= 2)
        {
            Handles.color = AdhesionPathColor;
            Handles.DrawAAPolyLine(3.5f, s_path.ToArray());
        }

        // desired：当前帧连线目标点
        Handles.color = DesiredColor;
        Handles.SphereHandleCap(0, desiredWorld, Quaternion.identity, 0.12f, EventType.Repaint);
        Handles.Label(desiredWorld + Vector3.up * 0.28f, "Desired");

        // 纯烘焙落点 vs 吸附后落点
        Handles.color = BakedOnlyColor;
        Handles.SphereHandleCap(0, bakedOnlyWorld, Quaternion.identity, 0.08f, EventType.Repaint);
        Handles.color = AdhesionPathColor;
        Handles.SphereHandleCap(0, adhesionActorWorld, Quaternion.identity, 0.1f, EventType.Repaint);
        Handles.color = new Color(0.35f, 1f, 0.45f, 0.65f);
        Handles.DrawLine(bakedOnlyWorld, adhesionActorWorld);
        Handles.DrawLine(adhesionActorWorld, desiredWorld);

        Handles.color = Color.white;
        Handles.Label(
            adhesionActorWorld + Vector3.up * 0.35f,
            $"Adhesion f={previewFrame}\nGreen=修正后  Cyan点=仅Bake  Yellow=Desired");
    }

    /// <summary>假敌球 + 地面圆 + 标签。</summary>
    public static void DrawEnemyMarker(Vector3 enemyWorld, MotionModifierMode mode)
    {
        Handles.color = EnemyColor;
        Handles.SphereHandleCap(0, enemyWorld, Quaternion.identity, EnemySphereRadius * 2f, EventType.Repaint);
        Handles.DrawWireDisc(enemyWorld, Vector3.up, EnemySphereRadius);
        string label = mode == MotionModifierMode.TargetAdhesion
            ? "Preview Enemy (drag)"
            : $"Preview Enemy ({mode})";
        Handles.Label(enemyWorld + Vector3.up * 0.4f, label);
    }

    /// <summary>
    /// 从帧 0 模拟到 endFrame：每帧先加 Bake Δ，再在窗内叠 Adhesion 修正。
    /// path 含起点与每帧结束后的落点。
    /// </summary>
    public static void SimulateThroughFrame(
        ActionDefinition action,
        in ActionMotionAdhesionParams window,
        Vector3 originPosition,
        Quaternion originRotation,
        Vector3 enemyWorld,
        float actorYawDegrees,
        int endFrame,
        List<Vector3> pathWorld,
        out Vector3 actorWorld,
        out Vector3 desiredWorld,
        out Vector3 bakedOnlyWorld)
    {
        pathWorld?.Clear();
        actorWorld = originPosition;
        bakedOnlyWorld = actorWorld;
        desiredWorld = new Vector3(enemyWorld.x, originPosition.y, enemyWorld.z);

        int enemyXMm = MotionQuantization.MetersToMm(enemyWorld.x);
        int enemyZMm = MotionQuantization.MetersToMm(enemyWorld.z);
        int actorXMm = MotionQuantization.MetersToMm(actorWorld.x);
        int actorZMm = MotionQuantization.MetersToMm(actorWorld.z);
        int bakedXMm = actorXMm;
        int bakedZMm = actorZMm;

        pathWorld?.Add(actorWorld);

        int lastFrame = Mathf.Max(0, endFrame);
        ActionBakedMotion baked = action != null ? action.BakedMotion : null;
        bool hasBaked = baked != null && baked.IsReady && baked.frameCount > 0;

        for (int frame = 0; frame <= lastFrame; frame++)
        {
            if (hasBaked && frame < baked.frameCount)
            {
                int dx = baked.positionDeltaMmX[frame];
                int dz = baked.positionDeltaMmZ[frame];
                ActionBakedMotion.ApplyPlanarMode(baked.planarMode, ref dx, ref dz);
                // 本地 Δ → 预览原点朝向的世界 Δ（与轨迹绘制一致）
                Vector3 worldDelta = originRotation * new Vector3(
                    MotionQuantization.MmToMeters(dx),
                    0f,
                    MotionQuantization.MmToMeters(dz));
                int wdx = MotionQuantization.MetersToMm(worldDelta.x);
                int wdz = MotionQuantization.MetersToMm(worldDelta.z);
                actorXMm += wdx;
                actorZMm += wdz;
                bakedXMm += wdx;
                bakedZMm += wdz;
            }

            if (window.IsActiveAtFrame(frame)
                && ActionMotionAdhesion.TryComputeCorrectionMm(
                    actorXMm,
                    actorZMm,
                    actorYawDegrees,
                    enemyXMm,
                    enemyZMm,
                    in window,
                    frame,
                    out int cx,
                    out int cz))
            {
                actorXMm += cx;
                actorZMm += cz;
            }

            actorWorld = new Vector3(
                MotionQuantization.MmToMeters(actorXMm),
                originPosition.y,
                MotionQuantization.MmToMeters(actorZMm));
            pathWorld?.Add(actorWorld);
        }

        bakedOnlyWorld = new Vector3(
            MotionQuantization.MmToMeters(bakedXMm),
            originPosition.y,
            MotionQuantization.MmToMeters(bakedZMm));

        if (ActionMotionAdhesion.TryBuildDesiredMm(
                actorXMm,
                actorZMm,
                enemyXMm,
                enemyZMm,
                window.HorizontalOffsetMm,
                window.LateralOffsetMm,
                out int desiredXMm,
                out int desiredZMm))
        {
            desiredWorld = new Vector3(
                MotionQuantization.MmToMeters(desiredXMm),
                originPosition.y,
                MotionQuantization.MmToMeters(desiredZMm));
        }
        else
        {
            // 与敌人重合时无法建连线，desired 退回敌心
            desiredWorld = new Vector3(enemyWorld.x, originPosition.y, enemyWorld.z);
        }
    }
}

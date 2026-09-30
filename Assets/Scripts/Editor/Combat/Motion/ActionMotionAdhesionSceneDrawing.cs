using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Action Editor Scene：假敌球 + TargetAdhesion 修正轨迹预览（与运行时共用位移重映射计算）。
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
            return new ActionMotionAdhesionParams(0, -1, 0, 0, 1, 0, 0);

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
        out Vector3 adhesionActorWorld,
        bool drawLabels = true,
        int actorRadiusMm = 280,
        int enemyRadiusMm = 280)
    {
        adhesionActorWorld = originPosition;
        bool stop = action != null && action.ExecutionPolicy.BodyCollisionMode == ActionBodyCollisionMode.StopOnContact;
        if (window == null && !stop)
            return;

        DrawEnemyMarker(enemyWorld, window != null ? window.Mode : MotionModifierMode.TargetAdhesion, drawLabels);
        Handles.color = EnemyColor;
        Handles.DrawWireDisc(enemyWorld, Vector3.up, enemyRadiusMm / 1000f);

        if (window != null && window.Mode != MotionModifierMode.TargetAdhesion && !stop)
            return;

        ActionMotionAdhesionParams adhesion = ToParams(window != null && window.Mode == MotionModifierMode.TargetAdhesion ? window : null);
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
            out Vector3 bakedOnlyWorld, actorRadiusMm, enemyRadiusMm);

        if (s_path.Count >= 2)
        {
            Handles.color = AdhesionPathColor;
            Handles.DrawAAPolyLine(3.5f, s_path.ToArray());
        }

        // desired：当前帧连线目标点
        Handles.color = DesiredColor;
        Handles.SphereHandleCap(0, desiredWorld, Quaternion.identity, 0.12f, EventType.Repaint);
        if (drawLabels) Handles.Label(desiredWorld + Vector3.up * 0.28f, "Desired");

        // 纯烘焙落点 vs 吸附后落点
        Handles.color = BakedOnlyColor;
        Handles.SphereHandleCap(0, bakedOnlyWorld, Quaternion.identity, 0.08f, EventType.Repaint);
        Handles.color = AdhesionPathColor;
        Handles.SphereHandleCap(0, adhesionActorWorld, Quaternion.identity, 0.1f, EventType.Repaint);
        Handles.color = new Color(0.35f, 1f, 0.45f, 0.65f);
        Handles.DrawLine(bakedOnlyWorld, adhesionActorWorld);
        Handles.DrawLine(adhesionActorWorld, desiredWorld);
        if (stop)
        {
            Handles.DrawWireDisc(adhesionActorWorld, Vector3.up, actorRadiusMm / 1000f);
            Handles.DrawLine(originPosition, bakedOnlyWorld);
            if (drawLabels) Handles.Label(originPosition + Vector3.up * .6f,
                "Body preview: Start / Cyan=requested base / Green=safe; obstacle Id=1 (fake), no scene walls");
        }

        Handles.color = Color.white;
        if (drawLabels) Handles.Label(
            adhesionActorWorld + Vector3.up * 0.35f,
            $"Adhesion f={previewFrame}\nGreen=修正后  Cyan点=仅Bake  Yellow=Desired");
    }

    /// <summary>假敌球 + 地面圆 + 标签。</summary>
    public static void DrawEnemyMarker(Vector3 enemyWorld, MotionModifierMode mode, bool drawLabels = true)
    {
        Handles.color = EnemyColor;
        Handles.SphereHandleCap(0, enemyWorld, Quaternion.identity, EnemySphereRadius * 2f, EventType.Repaint);
        Handles.DrawWireDisc(enemyWorld, Vector3.up, EnemySphereRadius);
        string label = mode == MotionModifierMode.TargetAdhesion
            ? "Preview Enemy (drag)"
            : $"Preview Enemy ({mode})";
        if (drawLabels) Handles.Label(enemyWorld + Vector3.up * 0.4f, label);
    }

    /// <summary>
    /// 从帧 0 模拟到 endFrame：窗内将基础位移重映射到目标落点，窗外恢复基础位移。
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
        out Vector3 bakedOnlyWorld,
        int actorRadiusMm = 280,
        int enemyRadiusMm = 280)
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
        var source = action != null ? ActionMotionRuntimePolicy.Resolve(action.ExecutionPolicy.BaseMotionMode,
            baked != null && baked.IsReady, action.Timeline.HasScriptedMovement) : ActionDisplacementSource.None;
        var state = new ActionMotionAdhesion.State();
        var bodies = new[] { new SimBodyObstacle(new SimActorId(1), new SimVec2(enemyXMm, enemyZMm), enemyRadiusMm) };
        for (int frame = 0; frame <= lastFrame; frame++)
        {
            SimVec2 delta = PreviewBaseDelta(action, source, frame, originRotation);
            bakedXMm += delta.X; bakedZMm += delta.Z;
            double progress = ActionMotionAdhesion.BakedProgress(
                source == ActionDisplacementSource.BakedMotion ? baked : null, frame, window.EndFrame);
            if (source == ActionDisplacementSource.ScriptedTimeline && window.IsActiveAtFrame(frame))
            {
                double total = 0, current = 0;
                for (int i = frame; i <= window.EndFrame; i++)
                {
                    SimVec2 step = PreviewBaseDelta(action, source, i, originRotation);
                    double length = System.Math.Sqrt((double)step.X * step.X + (double)step.Z * step.Z);
                    if (i == frame) current = length;
                    total += length;
                }
                if (total > 0) progress = current / total;
            }
            ActionMotionAdhesion.TryComputeDisplacementMm(ref state, actorXMm, actorZMm,
                actorYawDegrees, enemyXMm, enemyZMm, in window, frame,
                delta.X, delta.Z, progress, out int moveX, out int moveZ);
            if (action != null && action.ExecutionPolicy.BodyCollisionMode == ActionBodyCollisionMode.StopOnContact)
            {
                SimVec2 end = ActionBodySweep.Resolve(OpenFieldSimCollisionWorld.Instance,
                    new SimVec2(actorXMm, actorZMm), new SimVec2(moveX, moveZ), actorRadiusMm,
                    action.ExecutionPolicy.BodyContactSkinMm, bodies, out _, out _);
                actorXMm = end.X; actorZMm = end.Z;
            }
            else { actorXMm += moveX; actorZMm += moveZ; }
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

        if (state.Acquired)
            desiredWorld = new Vector3(MotionQuantization.MmToMeters(state.DesiredXMm), originPosition.y,
                MotionQuantization.MmToMeters(state.DesiredZMm));
    }

    // 预览与运行时一样按显式位移源取样；不把非 Baked 模式中的残留表误算为基础位移。
    static SimVec2 PreviewBaseDelta(ActionDefinition action, ActionDisplacementSource source, int frame, Quaternion rotation)
    {
        if (action == null || frame >= action.TotalFrames) return SimVec2.Zero;
        Vector3 local = Vector3.zero;
        if (source == ActionDisplacementSource.BakedMotion && action.BakedMotion.TryGetDelta(frame, out var baked, out _))
            local = new Vector3(baked.X, 0, baked.Z);
        else if (source == ActionDisplacementSource.ScriptedTimeline)
        {
            var movement = action.GetActiveMovementStateAtFrame(frame);
            if (movement == null) return SimVec2.Zero;
            Vector3 forward = rotation * Vector3.forward;
            forward.y = 0;
            if (forward.sqrMagnitude < .0001f) return SimVec2.Zero;
            Vector3 world = forward.normalized * (movement.ResolveSpeed(action.SampleRate) / ActionSim.LogicHz);
            if (world.sqrMagnitude < .0000001f) return SimVec2.Zero;
            return new SimVec2(MotionQuantization.MetersToMm(world.x), MotionQuantization.MetersToMm(world.z));
        }
        CharacterMotorSim.RotateLocalToWorld(MotionQuantization.DegreesToMilliDeg(rotation.eulerAngles.y),
            (int)System.Math.Round(local.x, System.MidpointRounding.AwayFromZero),
            (int)System.Math.Round(local.z, System.MidpointRounding.AwayFromZero), out int x, out int z);
        return new SimVec2(x, z);
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>ActionDefinition 30Hz→60Hz 迁移与 60Hz 就绪校验（Editor 显式执行）。</summary>
public static class ActionDefinition60HzMigrator
{
    static readonly string[] PointArrays =
    {
        "actionEvents",
        "playVfxNotifies",
        "playSfxStates",
    };

    static readonly string[] IntervalArrays =
    {
        "hitboxStates",
        "hurtboxStates",
        "cancelWindowStates",
        "phaseStates",
        "movementStates",
        "rotationStates",
        // Camera 窗虽不进入 Sim Runner，仍以同一动作逻辑帧配置，迁移时必须同步缩放。
        "cameraShotStates",
    };

    /// <summary>扫描项目内全部 ActionDefinition，报告 60Hz / 可模拟就绪状态（不改资产）。</summary>
    [MenuItem("ACT/Tools/Validate Action 60Hz Readiness")]
    public static void ValidateReadiness()
    {
        string[] guids = AssetDatabase.FindAssets("t:ActionDefinition");
        Array.Sort(guids, StringComparer.Ordinal);

        int total = 0;
        int ready = 0;
        int rate60 = 0;
        int pending30 = 0;
        var blockers = new List<string>(8);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ActionDefinition action = AssetDatabase.LoadAssetAtPath<ActionDefinition>(path);
            if (action == null)
                continue;

            total++;
            if (action.SampleRate == ActionSim.LogicHz)
                rate60++;
            if (ActionHzMigrationRules.ShouldMigrate(action.SampleRate))
                pending30++;

            if (action.IsSimulationReady)
            {
                ready++;
                continue;
            }

            if (blockers.Count < 12)
            {
                string reason = DescribeNotReady(action);
                blockers.Add($"{path} — {reason}");
            }
        }

        string summary =
            $"ActionDefinition 总数={total}\n"
            + $"sampleRate={ActionSim.LogicHz}：{rate60}\n"
            + $"仍为 30Hz（可迁移）：{pending30}\n"
            + $"IsSimulationReady：{ready}\n"
            + $"未就绪：{total - ready}";

        if (blockers.Count > 0)
        {
            summary += "\n\n未就绪示例：\n- " + string.Join("\n- ", blockers);
            if (total - ready > blockers.Count)
                summary += $"\n… 另有 {total - ready - blockers.Count} 项未列出";
        }

        if (pending30 == 0 && rate60 == total)
            summary += "\n\n30→60Hz 迁移：无需再跑 Migrate（无 sampleRate=30）。";
        else if (pending30 > 0)
            summary += "\n\n请执行 ACT/Tools/Migrate Action Assets 30Hz to 60Hz。";

        Debug.Log("Action 60Hz Readiness\n" + summary);
        EditorUtility.DisplayDialog("Action 60Hz Readiness", summary, "OK");
    }

    /// <summary>扫描全部 30Hz Action，确认后迁移动作帧与对应 Graph AtFrame 条件。</summary>
    [MenuItem("ACT/Tools/Migrate Action Assets 30Hz to 60Hz")]
    public static void MigrateAll()
    {
        List<ActionDefinition> actions = CollectActionsToMigrate();
        if (actions.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Action 60Hz Migration",
                "未发现 sampleRate=30 的 ActionDefinition；没有执行任何修改。\n"
                    + "可用 ACT/Tools/Validate Action 60Hz Readiness 查看就绪报告。",
                "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Action 60Hz Migration",
            $"将迁移 {actions.Count} 个 ActionDefinition，并同步其 Graph AtFrame 条件。\n"
                + "此操作支持当前 Unity Undo，但建议先确认 Git 工作区可回退。",
            "Migrate",
            "Cancel");
        if (!confirmed)
            return;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Migrate Action Assets 30Hz to 60Hz");
        var migratedSet = new HashSet<ActionDefinition>(actions);
        int graphCount = 0;

        try
        {
            for (int i = 0; i < actions.Count; i++)
                MigrateAction(actions[i]);

            graphCount = MigrateRelatedGraphs(migratedSet);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }

        Debug.Log(
            $"Action 60Hz Migration 完成：ActionDefinition={actions.Count}, ActionGraph={graphCount}。"
            + " 请立即运行 ActionSim EditMode 测试并在 Play Mode 回归连招、Hitbox、VFX/SFX 与位移。");
    }

    /// <summary>用简短中文说明为何尚不能进权威模拟。</summary>
    static string DescribeNotReady(ActionDefinition action)
    {
        if (action == null)
            return "空引用";
        if (ActionHzMigrationRules.ShouldMigrate(action.SampleRate))
            return "sampleRate=30，需迁移";
        if (action.SampleRate != ActionSim.LogicHz)
            return $"sampleRate={action.SampleRate}，期望 {ActionSim.LogicHz}";
        if (!action.HasAnimation)
            return "无动画段";
        if (action.TotalFrames <= 0)
            return "totalFrames<=0";
        return "IsSimulationReady=false";
    }

    /// <summary>按稳定资产路径收集尚未迁移的 30Hz 动作。</summary>
    static List<ActionDefinition> CollectActionsToMigrate()
    {
        string[] guids = AssetDatabase.FindAssets("t:ActionDefinition");
        Array.Sort(guids, StringComparer.Ordinal);
        var result = new List<ActionDefinition>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ActionDefinition action = AssetDatabase.LoadAssetAtPath<ActionDefinition>(path);
            if (action != null && ActionHzMigrationRules.ShouldMigrate(action.SampleRate))
                result.Add(action);
        }

        return result;
    }

    /// <summary>迁移单个动作的动画段、Timeline、HitStop 帧数与总帧数。</summary>
    static void MigrateAction(ActionDefinition action)
    {
        if (action == null || !ActionHzMigrationRules.ShouldMigrate(action.SampleRate))
            return;

        Undo.RecordObject(action, "Migrate Action to 60Hz");
        var serialized = new SerializedObject(action);
        SerializedProperty segments = serialized.FindProperty("animationSegments");
        MigrateAnimationSegments(segments);

        SerializedProperty timeline = serialized.FindProperty("timeline");
        if (timeline != null)
        {
            for (int i = 0; i < PointArrays.Length; i++)
                MigrateTimelineArray(timeline.FindPropertyRelative(PointArrays[i]), isPoint: true);
            for (int i = 0; i < IntervalArrays.Length; i++)
                MigrateTimelineArray(timeline.FindPropertyRelative(IntervalArrays[i]), isPoint: false);

            MigrateHitStopFrames(timeline.FindPropertyRelative("hitboxStates"));
        }

        serialized.FindProperty("sampleRate").intValue = ActionSim.LogicHz;
        serialized.FindProperty("totalFrames").intValue =
            ComputeTotalFramesAt60Hz(segments);
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(action);
    }

    /// <summary>把每个动画段的闭区间映射到 60Hz；-1 仍表示使用 Clip 末帧。</summary>
    static void MigrateAnimationSegments(SerializedProperty segments)
    {
        if (segments == null || !segments.isArray)
            return;

        for (int i = 0; i < segments.arraySize; i++)
        {
            SerializedProperty segment = segments.GetArrayElementAtIndex(i);
            SerializedProperty start = segment.FindPropertyRelative("startFrame");
            SerializedProperty end = segment.FindPropertyRelative("endFrame");
            if (start == null || end == null)
                continue;

            start.intValue = ActionHzMigrationRules.MapPointFrame(start.intValue);
            if (end.intValue >= 0)
                end.intValue = checked(end.intValue * 2 + 1);
        }
    }

    /// <summary>按点事件或闭区间规则迁移 Timeline 数组。</summary>
    static void MigrateTimelineArray(SerializedProperty array, bool isPoint)
    {
        if (array == null || !array.isArray)
            return;

        for (int i = 0; i < array.arraySize; i++)
        {
            SerializedProperty item = array.GetArrayElementAtIndex(i);
            SerializedProperty start = item.FindPropertyRelative("startFrame");
            SerializedProperty end = item.FindPropertyRelative("endFrame");
            if (start == null || end == null)
                continue;

            if (isPoint)
            {
                int mapped = ActionHzMigrationRules.MapPointFrame(start.intValue);
                start.intValue = mapped;
                end.intValue = mapped;
            }
            else
            {
                ActionHzMigrationRules.MapClosedInterval(
                    start.intValue,
                    end.intValue,
                    out int mappedStart,
                    out int mappedEnd);
                start.intValue = mappedStart;
                end.intValue = mappedEnd;
            }
        }
    }

    /// <summary>HitStop 为逻辑帧数，30→60 时帧数同步翻倍以保持墙钟时长。</summary>
    static void MigrateHitStopFrames(SerializedProperty hitboxes)
    {
        if (hitboxes == null || !hitboxes.isArray)
            return;

        for (int i = 0; i < hitboxes.arraySize; i++)
        {
            SerializedProperty feedback = hitboxes.GetArrayElementAtIndex(i)
                .FindPropertyRelative("payload")
                ?.FindPropertyRelative("feedback");
            SerializedProperty frames = feedback?.FindPropertyRelative("hitStopFrames");
            if (frames != null && frames.intValue > 0)
                frames.intValue = checked(frames.intValue * 2);
        }
    }

    /// <summary>按迁移后的段范围与 60Hz Clip 长度重算动作终止哨兵。</summary>
    static int ComputeTotalFramesAt60Hz(SerializedProperty segments)
    {
        if (segments == null || !segments.isArray)
            return 1;

        int total = 0;
        for (int i = 0; i < segments.arraySize; i++)
        {
            SerializedProperty segment = segments.GetArrayElementAtIndex(i);
            AnimationClip clip =
                segment.FindPropertyRelative("clip")?.objectReferenceValue as AnimationClip;
            if (clip == null)
                continue;

            int clipLastFrame = Mathf.Max(
                0,
                Mathf.RoundToInt(clip.length * ActionSim.LogicHz) - 1);
            int start = Mathf.Clamp(
                segment.FindPropertyRelative("startFrame").intValue,
                0,
                clipLastFrame);
            int serializedEnd = segment.FindPropertyRelative("endFrame").intValue;
            int end = serializedEnd < 0
                ? clipLastFrame
                : Mathf.Clamp(serializedEnd, start, clipLastFrame);
            total = checked(total + end - start + 1);
        }

        return Mathf.Max(1, total);
    }

    /// <summary>只迁移来源节点动作属于本批次的 AtFrame，保证重复运行与部分迁移均安全。</summary>
    static int MigrateRelatedGraphs(HashSet<ActionDefinition> migratedActions)
    {
        string[] guids = AssetDatabase.FindAssets("t:ActionGraph");
        Array.Sort(guids, StringComparer.Ordinal);
        int changedGraphs = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ActionGraph graph = AssetDatabase.LoadAssetAtPath<ActionGraph>(path);
            if (graph != null && MigrateGraph(graph, migratedActions))
                changedGraphs++;
        }

        return changedGraphs;
    }

    /// <summary>迁移单张图中属于本批次动作节点的 AtFrame 条件。</summary>
    static bool MigrateGraph(
        ActionGraph graph,
        HashSet<ActionDefinition> migratedActions)
    {
        var serialized = new SerializedObject(graph);
        SerializedProperty nodes = serialized.FindProperty("nodes");
        bool changed = false;
        for (int i = 0; nodes != null && i < nodes.arraySize; i++)
        {
            SerializedProperty node = nodes.GetArrayElementAtIndex(i);
            ActionDefinition action =
                node.FindPropertyRelative("action")?.objectReferenceValue as ActionDefinition;
            if (action == null || !migratedActions.Contains(action))
                continue;

            SerializedProperty transitions = node.FindPropertyRelative("automaticTransitions");
            for (int j = 0; transitions != null && j < transitions.arraySize; j++)
            {
                SerializedProperty transition = transitions.GetArrayElementAtIndex(j);
                SerializedProperty condition = transition.FindPropertyRelative("condition");
                SerializedProperty startFrame = transition.FindPropertyRelative("startFrame");
                if (condition == null
                    || startFrame == null
                    || condition.enumValueIndex != (int)ActionTransitionCondition.AtFrame)
                {
                    continue;
                }

                startFrame.intValue =
                    ActionHzMigrationRules.MapPointFrame(startFrame.intValue);
                changed = true;
            }
        }

        if (!changed)
            return false;

        Undo.RecordObject(graph, "Migrate Action Graph AtFrame to 60Hz");
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(graph);
        return true;
    }
}

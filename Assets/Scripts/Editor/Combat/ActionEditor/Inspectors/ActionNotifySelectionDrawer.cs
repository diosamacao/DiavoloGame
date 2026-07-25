using UnityEditor;
using UnityEngine;

/// <summary>右侧选中窗口细节面板；按类型绘制字段，帧数字与轨道双向同步。</summary>
public static class ActionNotifySelectionDrawer
{
    /// <summary>绘制选中窗口；无选中时显示动作基础字段。</summary>
    public static void Draw(Rect rect, SerializedObject so, ActionEditorSelection selection, ActionDefinition action)
    {
        GUILayout.BeginArea(rect);

        if (action == null || so == null)
        {
            EditorGUILayout.HelpBox("选中招式后可编辑细节。", MessageType.Info);
            GUILayout.EndArea();
            return;
        }

        so.Update();

        if (!selection.IsValid)
        {
            DrawActionBasics(so, action);
            GUILayout.EndArea();
            return;
        }

        EditorGUI.BeginChangeCheck();

        if (selection.Kind == ActionTimelineTrackKind.Animation)
        {
            DrawAnimationSegment(selection.ElementProperty, action);
        }
        else
        {
            bool pointEvent = ActionEditorStyles.IsPointEventTrack(selection.Kind);
            EditorGUILayout.LabelField(pointEvent ? "Event" : "Window", EditorStyles.boldLabel);
            SerializedProperty element = selection.ElementProperty;

            DrawFrameFields(element, action, pointEvent);
            EditorGUILayout.PropertyField(element.FindPropertyRelative("id"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("priority"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("trackName"));

            switch (selection.Kind)
            {
                case ActionTimelineTrackKind.Hitbox:
                    DrawHitbox(element);
                    break;
                case ActionTimelineTrackKind.Hurtbox:
                    DrawHurtbox(element);
                    break;
                case ActionTimelineTrackKind.Vfx:
                    DrawVfx(element, action);
                    break;
                case ActionTimelineTrackKind.Sfx:
                    DrawSfx(element, action);
                    break;
                case ActionTimelineTrackKind.Cancel:
                    DrawCancel(element);
                    break;
                case ActionTimelineTrackKind.Phase:
                    DrawPhase(element);
                    break;
                case ActionTimelineTrackKind.Movement:
                    DrawMovement(element);
                    break;
                case ActionTimelineTrackKind.Rotation:
                    DrawRotation(element);
                    break;
                case ActionTimelineTrackKind.Event:
                    DrawEvent(element);
                    break;
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
        }

        GUILayout.EndArea();
    }

    /// <summary>选中 Animation 段时编辑 Clip / 裁切 / 淡入。</summary>
    static void DrawAnimationSegment(SerializedProperty element, ActionDefinition action)
    {
        EditorGUILayout.LabelField("Animation Segment", EditorStyles.boldLabel);
        if (element == null)
            return;

        EditorGUILayout.PropertyField(element.FindPropertyRelative("clip"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("startFrame"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("endFrame"), new GUIContent("End Frame", "<0 = 用到 Clip 末尾"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("crossFadeDuration"));

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("Total Frames (Action)", action != null ? action.TotalFrames : 0);
        }

        EditorGUILayout.HelpBox("段在时间轴上的长度由 Clip 有效帧累加；修改 Clip/裁切后 Total Frames 会自动更新。", MessageType.None);
    }

    /// <summary>未选中窗口时编辑招式基础字段（采样率等）。</summary>
    static void DrawActionBasics(SerializedObject so, ActionDefinition action)
    {
        EditorGUILayout.LabelField("Action", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("选中 Animation 轨上的段或其它窗口可编辑细节。显示名即资产文件名。", MessageType.None);

        EditorGUI.BeginChangeCheck();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.TextField("File Name", action.name);
        EditorGUILayout.PropertyField(so.FindProperty("sampleRate"));
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.IntField("Total Frames", action.TotalFrames);
        EditorGUILayout.PropertyField(so.FindProperty("actionType"));
        EditorGUILayout.PropertyField(so.FindProperty("crossFadeDuration"));

        if (!action.HasAnimation)
            EditorGUILayout.HelpBox("请在 Animation 轨添加并绑定 Clip，否则无法预览 Pose。", MessageType.Warning);

        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
        }
    }

    /// <summary>点事件只编辑触发帧；区间窗口编辑起止帧。</summary>
    static void DrawFrameFields(SerializedProperty element, ActionDefinition action, bool pointEvent)
    {
        int maxFrame = Mathf.Max(0, action.TotalFrames - 1);
        SerializedProperty startProp = element.FindPropertyRelative("startFrame");
        SerializedProperty endProp = element.FindPropertyRelative("endFrame");
        if (startProp == null || endProp == null)
            return;

        if (pointEvent)
        {
            startProp.intValue = EditorGUILayout.IntSlider("Trigger Frame", startProp.intValue, 0, maxFrame);
            endProp.intValue = startProp.intValue;
            return;
        }

        startProp.intValue = EditorGUILayout.IntSlider("Start Frame", startProp.intValue, 0, maxFrame);
        endProp.intValue = EditorGUILayout.IntSlider(
            "End Frame",
            Mathf.Max(endProp.intValue, startProp.intValue),
            startProp.intValue,
            maxFrame);
    }

    static void DrawHitbox(SerializedProperty element)
    {
        EditorGUILayout.PropertyField(element.FindPropertyRelative("hitboxId"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("shape"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("attachPointId"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("localOffset"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("localEulerAngles"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("size"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("damageWeight"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("hitReactionId"));
    }

    static void DrawHurtbox(SerializedProperty element)
    {
        EditorGUILayout.PropertyField(element.FindPropertyRelative("hurtboxId"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("shape"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("attachPointId"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("localOffset"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("localEulerAngles"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("size"));
    }

    static void DrawVfx(SerializedProperty element, ActionDefinition action)
    {
        EditorGUILayout.PropertyField(element.FindPropertyRelative("prefab"));
        EditorGUILayout.PropertyField(
            element.FindPropertyRelative("attachPointId"),
            new GUIContent("Attach Point Id", "模型子节点名；空则用角色默认挂点"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("localOffset"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("localEulerAngles"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("localScale"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("parentToAttachPoint"));
        DrawPlaybackSpeed(element, action, isVfx: true);
    }

    static void DrawSfx(SerializedProperty element, ActionDefinition action)
    {
        EditorGUILayout.PropertyField(element.FindPropertyRelative("audioClip"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("volume"));
        DrawPlaybackSpeed(element, action, isVfx: false);
    }

    static void DrawCancel(SerializedProperty element)
    {
        EditorGUILayout.PropertyField(
            element.FindPropertyRelative("windowType"),
            new GUIContent(
                "Window Type",
                "Normal 为普通派生；Perfect 与 Normal 重叠且 Trigger 相同时优先。"));
    }

    /// <summary>绘制语义阶段；Recovery 额外集成移动取消与 Graph Entry 重开能力。</summary>
    static void DrawPhase(SerializedProperty element)
    {
        SerializedProperty kindProp = element.FindPropertyRelative("kind");
        EditorGUILayout.PropertyField(kindProp);
        ActionPhaseKind kind = kindProp != null
            ? (ActionPhaseKind)kindProp.intValue
            : ActionPhaseKind.Startup;
        bool controlsInterruptibility =
            kind is ActionPhaseKind.Startup or ActionPhaseKind.Active or ActionPhaseKind.Recovery;
        if (controlsInterruptibility)
            EditorGUILayout.PropertyField(element.FindPropertyRelative("interruptible"));

        if (kind != ActionPhaseKind.Recovery)
        {
            return;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Recovery Exit", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            element.FindPropertyRelative("allowMovementCancel"),
            new GUIContent("Allow Movement Cancel", "有移动输入时退出 Action 返回 Locomotion。"));
        EditorGUILayout.PropertyField(
            element.FindPropertyRelative("allowEntryRestart"),
            new GUIContent("Allow Entry Restart", "有效动作输入按当前 ActionGraph Entry 重开。"));
    }

    static void DrawMovement(SerializedProperty element)
    {
        EditorGUILayout.PropertyField(element.FindPropertyRelative("displacementDistance"));
    }

    static void DrawRotation(SerializedProperty element)
    {
        EditorGUILayout.PropertyField(element.FindPropertyRelative("smoothTimeOverride"));
    }

    static void DrawEvent(SerializedProperty element)
    {
        EditorGUILayout.PropertyField(element.FindPropertyRelative("kind"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("payloadId"));
    }

    /// <summary>显式播放倍率；可选显示资源估测自然时长（只读，不驱动倍率）。</summary>
    static void DrawPlaybackSpeed(SerializedProperty element, ActionDefinition action, bool isVfx)
    {
        SerializedProperty speedProp = element.FindPropertyRelative("playbackSpeed");
        if (speedProp != null)
            speedProp.floatValue = EditorGUILayout.FloatField("Playback Speed", Mathf.Max(0.0001f, speedProp.floatValue));

        float natural = 0f;
        if (isVfx)
        {
            var prefab = element.FindPropertyRelative("prefab")?.objectReferenceValue as GameObject;
            if (prefab != null)
                natural = ActionVfxPlayback.EstimateNaturalDurationSeconds(prefab);
        }
        else
        {
            var clip = element.FindPropertyRelative("audioClip")?.objectReferenceValue as AudioClip;
            if (clip != null)
                natural = clip.length;
        }

        if (natural > 0f)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.FloatField("Estimated Duration (s)", natural);
        }
    }
}

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

        EditorGUILayout.LabelField("Window", EditorStyles.boldLabel);
        SerializedProperty element = selection.ElementProperty;
        EditorGUI.BeginChangeCheck();

        DrawFrameFields(element, action);
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

        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
        }

        GUILayout.EndArea();
    }

    /// <summary>未选中窗口时编辑招式基础字段（Clip / 采样率等）。</summary>
    static void DrawActionBasics(SerializedObject so, ActionDefinition action)
    {
        EditorGUILayout.LabelField("Action", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("选中时间轴窗口可编辑片段细节。", MessageType.None);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(so.FindProperty("displayName"));
        EditorGUILayout.PropertyField(so.FindProperty("id"));
        EditorGUILayout.PropertyField(so.FindProperty("animationClip"));
        EditorGUILayout.PropertyField(so.FindProperty("sampleRate"));
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.IntField("Total Frames", action.TotalFrames);
        EditorGUILayout.PropertyField(so.FindProperty("actionType"));
        EditorGUILayout.PropertyField(so.FindProperty("crossFadeDuration"));

        if (action.AnimationClip == null)
            EditorGUILayout.HelpBox("请指定 Animation Clip，否则无法预览 Pose。", MessageType.Warning);

        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
        }
    }

    static void DrawFrameFields(SerializedProperty element, ActionDefinition action)
    {
        int maxFrame = Mathf.Max(0, action.TotalFrames - 1);
        SerializedProperty startProp = element.FindPropertyRelative("startFrame");
        SerializedProperty endProp = element.FindPropertyRelative("endFrame");
        if (startProp == null || endProp == null)
            return;

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
        SerializedProperty prefabProp = element.FindPropertyRelative("prefab");
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(prefabProp);
        if (EditorGUI.EndChangeCheck() && prefabProp.objectReferenceValue is GameObject prefab)
        {
            float natural = ActionVfxPlayback.EstimateNaturalDurationSeconds(prefab);
            SerializedProperty naturalProp = element.FindPropertyRelative("naturalDurationSeconds");
            if (naturalProp != null)
                naturalProp.floatValue = natural;

            // 换 Prefab 时按自然时长重置窗口长度，避免旧长度与新资源错配。
            SerializedProperty startProp = element.FindPropertyRelative("startFrame");
            SerializedProperty endProp = element.FindPropertyRelative("endFrame");
            if (startProp != null && endProp != null)
            {
                int length = ActionVfxPlayback.DurationSecondsToFrameCount(natural, action.SampleRate);
                int maxFrame = Mathf.Max(0, action.TotalFrames - 1);
                endProp.intValue = Mathf.Min(maxFrame, startProp.intValue + length - 1);
            }
        }

        EditorGUILayout.PropertyField(element.FindPropertyRelative("localOffset"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("localEulerAngles"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("localScale"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("parentToAttachPoint"));
        DrawPlaybackReadouts(element, action);
    }

    static void DrawSfx(SerializedProperty element, ActionDefinition action)
    {
        SerializedProperty clipProp = element.FindPropertyRelative("audioClip");
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(clipProp);
        if (EditorGUI.EndChangeCheck() && clipProp.objectReferenceValue is AudioClip clip)
        {
            SerializedProperty naturalProp = element.FindPropertyRelative("naturalDurationSeconds");
            if (naturalProp != null)
                naturalProp.floatValue = clip.length;

            SerializedProperty startProp = element.FindPropertyRelative("startFrame");
            SerializedProperty endProp = element.FindPropertyRelative("endFrame");
            if (startProp != null && endProp != null)
            {
                int length = ActionVfxPlayback.DurationSecondsToFrameCount(clip.length, action.SampleRate);
                int maxFrame = Mathf.Max(0, action.TotalFrames - 1);
                endProp.intValue = Mathf.Min(maxFrame, startProp.intValue + length - 1);
            }
        }

        EditorGUILayout.PropertyField(element.FindPropertyRelative("volume"));
        DrawPlaybackReadouts(element, action);
    }

    static void DrawCancel(SerializedProperty element)
    {
        EditorGUILayout.PropertyField(element.FindPropertyRelative("cancelType"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("allowedInputs"), true);
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

    static void DrawPlaybackReadouts(SerializedProperty element, ActionDefinition action)
    {
        SerializedProperty naturalProp = element.FindPropertyRelative("naturalDurationSeconds");
        SerializedProperty startProp = element.FindPropertyRelative("startFrame");
        SerializedProperty endProp = element.FindPropertyRelative("endFrame");
        float natural = naturalProp != null ? naturalProp.floatValue : 0f;
        int frames = startProp != null && endProp != null
            ? Mathf.Max(1, endProp.intValue - startProp.intValue + 1)
            : 1;
        float windowSeconds = frames / action.SampleRate;
        float speed = natural > 0f ? natural / Mathf.Max(windowSeconds, 0.0001f) : 1f;

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.FloatField("Natural Duration (s)", natural);
            EditorGUILayout.FloatField("Window Duration (s)", windowSeconds);
            EditorGUILayout.FloatField("Playback Speed", speed);
        }
    }
}

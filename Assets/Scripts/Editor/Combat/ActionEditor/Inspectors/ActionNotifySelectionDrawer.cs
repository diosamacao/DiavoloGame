using UnityEditor;
using UnityEngine;

/// <summary>
/// 右侧选中窗口细节面板；按类型绘制字段，帧数字与轨道双向同步。
/// 同类型多选时，任一字段修改会批量写回全部选中窗口。
/// Hitbox 等长表单通过纵向 ScrollView 保证底部字段可编辑。
/// </summary>
public static class ActionNotifySelectionDrawer
{
    static Vector2 _scroll;

    /// <summary>绘制选中窗口；同类型多选支持批量改属性，混合类型仅改主选中项。</summary>
    public static void Draw(Rect rect, SerializedObject so, ActionEditorSelectionSet selectionSet, ActionDefinition action)
    {
        GUILayout.BeginArea(rect);
        // 右侧面板固定高度，内容超出时必须可纵向滚动（Hitbox Feedback 等字段很长）
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        if (action == null || so == null)
        {
            EditorGUILayout.HelpBox("选中招式后可编辑细节。", MessageType.Info);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        so.Update();

        ActionEditorSelection selection = selectionSet != null ? selectionSet.Primary : default;
        bool multiSameKind = selectionSet != null
            && selectionSet.Count > 1
            && AreAllSameKind(selectionSet);
        bool multiMixedKind = selectionSet != null
            && selectionSet.Count > 1
            && !multiSameKind;

        if (multiSameKind)
        {
            EditorGUILayout.HelpBox(
                $"已选中 {selectionSet.Count} 个同类型窗口。修改任意字段将应用到全部选中项。"
                + " 框选/Ctrl 点选均可；Ctrl+C/V 复制粘贴仍可用。",
                MessageType.Info);
        }
        else if (multiMixedKind)
        {
            EditorGUILayout.HelpBox(
                $"已选中 {selectionSet.Count} 个窗口（含不同类型）。下方仅编辑主选中项；同类型多选才可批量应用。",
                MessageType.Warning);
        }

        if (!selection.IsValid)
        {
            DrawActionBasics(so, action);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        // 仅同类型多选时开启批量写回
        ActionEditorSelectionSet batchSet = multiSameKind ? selectionSet : null;

        EditorGUI.BeginChangeCheck();

        if (selection.Kind == ActionTimelineTrackKind.Animation)
        {
            DrawAnimationSegment(selection.ElementProperty, action, batchSet);
        }
        else
        {
            bool pointEvent = ActionEditorStyles.IsPointEventTrack(selection.Kind);
            EditorGUILayout.LabelField(pointEvent ? "Event" : "Window", EditorStyles.boldLabel);
            SerializedProperty element = selection.ElementProperty;

            DrawFrameFields(element, action, pointEvent, batchSet);
            DrawMultiProperty(batchSet, element, "id");
            DrawMultiProperty(batchSet, element, "priority");
            DrawMultiProperty(batchSet, element, "trackName");

            switch (selection.Kind)
            {
                case ActionTimelineTrackKind.Hitbox:
                    DrawHitbox(element, batchSet);
                    break;
                case ActionTimelineTrackKind.Hurtbox:
                    DrawHurtbox(element, batchSet);
                    break;
                case ActionTimelineTrackKind.Vfx:
                    DrawVfx(element, action, batchSet);
                    break;
                case ActionTimelineTrackKind.Sfx:
                    DrawSfx(element, action, batchSet);
                    break;
                case ActionTimelineTrackKind.Cancel:
                    DrawCancel(element, batchSet);
                    break;
                case ActionTimelineTrackKind.Phase:
                    DrawPhase(element, batchSet);
                    break;
                case ActionTimelineTrackKind.Movement:
                    DrawMovement(element, batchSet);
                    break;
                case ActionTimelineTrackKind.Rotation:
                    DrawRotation(element, batchSet);
                    break;
                case ActionTimelineTrackKind.Event:
                    DrawEvent(element, batchSet);
                    break;
                case ActionTimelineTrackKind.PerfectDodgeWindow:
                    DrawPerfectDodgeWindow();
                    break;
                case ActionTimelineTrackKind.MotionModifier:
                    DrawMotionModifier(element, batchSet);
                    break;
                case ActionTimelineTrackKind.MotionCommand:
                    DrawMotionCommand(element, batchSet);
                    break;
                case ActionTimelineTrackKind.Camera:
                    EditorGUILayout.PropertyField(
                        so.FindProperty("timeline.cameraSettings"),
                        new GUIContent("Camera Track Settings"),
                        includeChildren: true);
                    DrawCameraShot(element, batchSet, selection, action);
                    break;
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
        }

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    /// <summary>选中 Animation 段时编辑 Clip / 裁切 / 淡入；同类型多选可批量。</summary>
    static void DrawAnimationSegment(
        SerializedProperty element,
        ActionDefinition action,
        ActionEditorSelectionSet batchSet)
    {
        EditorGUILayout.LabelField("Animation Segment", EditorStyles.boldLabel);
        if (element == null)
            return;

        DrawMultiProperty(batchSet, element, "clip");
        DrawMultiProperty(batchSet, element, "startFrame");
        DrawMultiProperty(
            batchSet,
            element,
            "endFrame",
            new GUIContent("End Frame", "<0 = 用到 Clip 末尾"));
        DrawMultiProperty(batchSet, element, "crossFadeDuration");

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
        EditorGUILayout.HelpBox(
            "选中 Animation 段或其它窗口可编辑细节。多选：拖拽框选 / Ctrl 点选 / Shift 同轨范围选；"
            + "同类型多选可批量改属性；Ctrl+C/V 复制粘贴（可跨 Action）。",
            MessageType.None);

        EditorGUI.BeginChangeCheck();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.TextField("File Name", action.name);
        EditorGUILayout.PropertyField(so.FindProperty("sampleRate"));
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.IntField("Total Frames", action.TotalFrames);
        EditorGUILayout.PropertyField(so.FindProperty("actionType"));
        EditorGUILayout.PropertyField(so.FindProperty("crossFadeDuration"));
        EditorGUILayout.PropertyField(
            so.FindProperty("executionPolicy"),
            new GUIContent("Execution Policy"),
            includeChildren: true);
        EditorGUILayout.PropertyField(
            so.FindProperty("timeline.cameraSettings"),
            new GUIContent("Camera Track Settings"),
            includeChildren: true);

        if (!action.HasAnimation)
            EditorGUILayout.HelpBox("请在 Animation 轨添加并绑定 Clip，否则无法预览 Pose。", MessageType.Warning);

        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
        }
    }

    /// <summary>点事件只编辑触发帧；区间窗口编辑起止帧；多选时批量同步。</summary>
    static void DrawFrameFields(
        SerializedProperty element,
        ActionDefinition action,
        bool pointEvent,
        ActionEditorSelectionSet batchSet)
    {
        int maxFrame = Mathf.Max(0, action.TotalFrames - 1);
        SerializedProperty startProp = element.FindPropertyRelative("startFrame");
        SerializedProperty endProp = element.FindPropertyRelative("endFrame");
        if (startProp == null || endProp == null)
            return;

        if (pointEvent)
        {
            DrawMultiIntSlider(batchSet, element, "startFrame", "Trigger Frame", 0, maxFrame);
            // 点事件结束帧跟随触发帧
            if (batchSet != null && batchSet.Count > 1)
            {
                for (int i = 0; i < batchSet.Items.Count; i++)
                {
                    SerializedProperty peer = batchSet.Items[i].ElementProperty;
                    if (peer == null)
                        continue;
                    SerializedProperty peerStart = peer.FindPropertyRelative("startFrame");
                    SerializedProperty peerEnd = peer.FindPropertyRelative("endFrame");
                    if (peerStart != null && peerEnd != null)
                        peerEnd.intValue = peerStart.intValue;
                }
            }
            else
            {
                endProp.intValue = startProp.intValue;
            }

            return;
        }

        DrawMultiIntSlider(batchSet, element, "startFrame", "Start Frame", 0, maxFrame);
        int startValue = element.FindPropertyRelative("startFrame").intValue;
        DrawMultiIntSlider(
            batchSet,
            element,
            "endFrame",
            "End Frame",
            startValue,
            maxFrame,
            clampMinToPrimaryStart: true);
    }

    static void DrawHitbox(SerializedProperty element, ActionEditorSelectionSet batchSet)
    {
        DrawMultiProperty(batchSet, element, "shape");
        DrawMultiProperty(batchSet, element, "attachPointId");
        DrawMultiProperty(batchSet, element, "localOffset");
        DrawMultiProperty(batchSet, element, "localEulerAngles");
        DrawMultiProperty(batchSet, element, "size");
        DrawMultiProperty(
            batchSet,
            element,
            "parentToAttachPoint",
            new GUIContent(
                "Parent To Attach Point",
                "勾选：每帧跟随挂点/角色根；取消：窗口进入帧写入世界空间后不再跟随（对齐 VFX）。"));
        DrawMultiProperty(
            batchSet,
            element,
            "payload",
            new GUIContent("Hit Payload"),
            includeChildren: true);
    }

    static void DrawHurtbox(SerializedProperty element, ActionEditorSelectionSet batchSet)
    {
        DrawMultiProperty(batchSet, element, "hurtboxId");
        DrawMultiProperty(batchSet, element, "shape");
        DrawMultiProperty(batchSet, element, "attachPointId");
        DrawMultiProperty(batchSet, element, "localOffset");
        DrawMultiProperty(batchSet, element, "localEulerAngles");
        DrawMultiProperty(batchSet, element, "size");
    }

    static void DrawVfx(SerializedProperty element, ActionDefinition action, ActionEditorSelectionSet batchSet)
    {
        DrawMultiProperty(batchSet, element, "prefab");
        DrawMultiProperty(
            batchSet,
            element,
            "attachPointId",
            new GUIContent("Attach Point Id", "模型子节点名；空则用角色默认挂点"));
        DrawMultiProperty(batchSet, element, "localOffset");
        DrawMultiProperty(batchSet, element, "localEulerAngles");
        DrawMultiProperty(batchSet, element, "localScale");
        DrawMultiProperty(
            batchSet,
            element,
            "parentToAttachPoint",
            new GUIContent(
                "Parent To Attach Point",
                "勾选：实例挂到挂点下并跟随；取消：在触发帧按挂点姿态写入世界空间后不再跟随（对齐运行时）。"));
        DrawPlaybackSpeed(element, action, isVfx: true, batchSet);
    }

    static void DrawSfx(SerializedProperty element, ActionDefinition action, ActionEditorSelectionSet batchSet)
    {
        DrawMultiProperty(batchSet, element, "audioClip");
        DrawMultiProperty(batchSet, element, "volume");
        DrawPlaybackSpeed(element, action, isVfx: false, batchSet);
    }

    static void DrawCancel(SerializedProperty element, ActionEditorSelectionSet batchSet)
    {
        DrawMultiProperty(
            batchSet,
            element,
            "windowType",
            new GUIContent(
                "Window Type",
                "Normal 为普通派生；Perfect 与 Normal 重叠且 Trigger 相同时优先。"));
    }

    /// <summary>绘制语义阶段；Recovery 额外集成移动取消与 Graph Entry 重开能力。</summary>
    static void DrawPhase(SerializedProperty element, ActionEditorSelectionSet batchSet)
    {
        DrawMultiProperty(batchSet, element, "kind");
        SerializedProperty kindProp = element.FindPropertyRelative("kind");
        ActionPhaseKind kind = kindProp != null
            ? (ActionPhaseKind)kindProp.intValue
            : ActionPhaseKind.Startup;
        bool controlsInterruptibility =
            kind is ActionPhaseKind.Startup or ActionPhaseKind.Active or ActionPhaseKind.Recovery;
        if (controlsInterruptibility)
            DrawMultiProperty(batchSet, element, "interruptible");

        if (kind != ActionPhaseKind.Recovery)
            return;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Recovery Exit", EditorStyles.boldLabel);
        DrawMultiProperty(
            batchSet,
            element,
            "allowMovementCancel",
            new GUIContent("Allow Movement Cancel", "有移动输入时退出 Action 返回 Locomotion。"));
        DrawMultiProperty(
            batchSet,
            element,
            "allowEntryRestart",
            new GUIContent("Allow Entry Restart", "有效动作输入按当前 ActionGraph Entry 重开。"));
    }

    static void DrawMovement(SerializedProperty element, ActionEditorSelectionSet batchSet)
    {
        DrawMultiProperty(batchSet, element, "displacementDistance");
    }

    static void DrawRotation(SerializedProperty element, ActionEditorSelectionSet batchSet)
    {
        DrawMultiProperty(batchSet, element, "smoothTimeOverride");
    }

    static void DrawEvent(SerializedProperty element, ActionEditorSelectionSet batchSet)
    {
        DrawMultiProperty(batchSet, element, "kind");
        DrawMultiProperty(batchSet, element, "payloadId");
    }

    /// <summary>完美闪避窗无额外载荷；语义由 Pipeline 消费。</summary>
    static void DrawPerfectDodgeWindow()
    {
        EditorGUILayout.HelpBox(
            "玩家 Dodge 上的完美闪避窗。窗内被命中：吞伤、不 Grant，并武装 PerfectDodgeCounter。\n"
            + "与 Phase=Invincible 不同：完美窗优先且会武装反击缓冲。",
            MessageType.Info);
    }

    /// <summary>位移修正窗：SoftBodySuppress / TargetAdhesion 字段。</summary>
    static void DrawMotionModifier(SerializedProperty element, ActionEditorSelectionSet batchSet)
    {
        DrawMultiProperty(batchSet, element, "mode");
        SerializedProperty modeProp = element.FindPropertyRelative("mode");
        bool adhesion = modeProp != null
            && modeProp.enumValueIndex == (int)MotionModifierMode.TargetAdhesion;

        if (adhesion)
        {
            EditorGUILayout.HelpBox(
                "TargetAdhesion：desired = 敌人 + normalize(敌−我)*horizontalOffset。"
                + " >0 穿到敌后侧；窗口时长=吸附时长（剩余帧均摊）。\n"
                + "Scene：选中本窗口后显示红色假敌球（可拖），绿色为吸附修正轨迹；Scrub 预览根跟修正落点。",
                MessageType.Info);
            DrawMultiProperty(batchSet, element, "targetSource");
            DrawMultiProperty(
                batchSet,
                element,
                "horizontalOffsetMm",
                new GUIContent("Horizontal Offset (mm)", ">0 敌后，=0 敌心，<0 敌前"));
            DrawMultiProperty(batchSet, element, "lateralOffsetMm");
            DrawMultiProperty(batchSet, element, "maxCorrectionMmPerFrame");
            DrawMultiProperty(batchSet, element, "maxAcquireDistanceMm");
            DrawMultiProperty(batchSet, element, "maxAngleMilliDeg");
            DrawMultiProperty(batchSet, element, "stopOnTargetLost");
        }
        else
        {
            EditorGUILayout.HelpBox(
                "SoftBodySuppress：窗内攻击者不参与角色软体互撞，仍碰静态墙。\n"
                + "Scene：选中本窗口时仍显示可拖拽假敌球，便于对照叠人距离（无吸附轨迹）。",
                MessageType.Info);
        }
    }

    /// <summary>离散位移点事件：Relocate / SnapFacing（运行时经 ActionMotionResolver）。</summary>
    static void DrawMotionCommand(SerializedProperty element, ActionEditorSelectionSet batchSet)
    {
        EditorGUILayout.HelpBox(
            "MotionCommand：触发帧执行 RelocateBehind / RelocateToOffset / SnapFacing。"
            + " 落在 Base+Adhesion 之后；需有效 ActionTarget（或 CurrentLock）。",
            MessageType.Info);
        DrawMultiProperty(batchSet, element, "commandType");
        DrawMultiProperty(batchSet, element, "targetSource");
        DrawMultiProperty(batchSet, element, "behindDistanceMm");
        DrawMultiProperty(batchSet, element, "localOffsetMm");
        DrawMultiProperty(batchSet, element, "facingPolicy");
        DrawMultiProperty(batchSet, element, "collisionPolicy");
        DrawMultiProperty(batchSet, element, "fallbackPolicy");
        DrawMultiProperty(batchSet, element, "forwardFallbackMm");
        DrawMultiProperty(batchSet, element, "softBodySuppressFrames");
        DrawMultiProperty(batchSet, element, "preserveVertical");
    }

    /// <summary>镜头区间：官方 Spline、模型无关 Binding、FOV 与反馈参数。</summary>
    static void DrawCameraShot(
        SerializedProperty element,
        ActionEditorSelectionSet batchSet,
        ActionEditorSelection selection,
        ActionDefinition action)
    {
        EditorGUILayout.HelpBox(
            "Camera 窗只由表现层 CameraShotPlayer 消费。Position Spline 是机位唯一真源；"
            + "关闭 Override Camera Pose 可只使用 Hold Follow / Impulse。",
            MessageType.Info);
        DrawMultiProperty(batchSet, element, "overrideCameraPose");
        DrawMultiProperty(batchSet, element, "referenceBinding", includeChildren: true);
        DrawCameraSplineCurveRule(element, batchSet, selection, action);
        DrawCameraPositionSpline(batchSet, element);
        DrawMultiProperty(batchSet, element, "speedCurve");
        DrawMultiProperty(batchSet, element, "constantSpeed");
        DrawMultiProperty(batchSet, element, "lookAtBinding", includeChildren: true);
        DrawMultiProperty(batchSet, element, "lookAtLocalPosition");
        DrawMultiProperty(batchSet, element, "fieldOfViewCurve");
        DrawMultiProperty(batchSet, element, "blendInSeconds");
        DrawMultiProperty(batchSet, element, "inheritPosition");
        DrawMultiProperty(batchSet, element, "holdFollow");
        DrawMultiProperty(batchSet, element, "impulseOnEnter");
    }

    /// <summary>切换端点几何规则并立即把同类型多选窗口的预设编译进各自 Spline。</summary>
    static void DrawCameraSplineCurveRule(
        SerializedProperty element,
        ActionEditorSelectionSet batchSet,
        ActionEditorSelection selection,
        ActionDefinition action)
    {
        SerializedProperty ruleProp = element.FindPropertyRelative("splineCurveRule");
        if (ruleProp == null)
            return;

        bool mixed = IsMixed(batchSet, element, "splineCurveRule");
        EditorGUI.showMixedValue = mixed;
        EditorGUI.BeginChangeCheck();
        var rule = (CameraSplineCurveRule)EditorGUILayout.EnumPopup(
            new GUIContent("Curve Rule", "预设规则只编辑首尾端点；Custom 开放全部 Knot 与 Tangent。"),
            (CameraSplineCurveRule)ruleProp.intValue);
        EditorGUI.showMixedValue = false;
        if (!EditorGUI.EndChangeCheck())
            return;

        SerializedObject so = element.serializedObject;
        Undo.RecordObject(so.targetObject, "Change Camera Spline Curve Rule");
        ruleProp.intValue = (int)rule;
        PropagateRelative(batchSet, "splineCurveRule");
        so.ApplyModifiedProperties();
        ApplyCameraSplineRules(action, selection, batchSet);
        EditorUtility.SetDirty(so.targetObject);
        so.Update();
    }

    /// <summary>按当前单选或同类型多选索引重建预设路径；Custom 保留原始作者数据。</summary>
    static void ApplyCameraSplineRules(
        ActionDefinition action,
        ActionEditorSelection selection,
        ActionEditorSelectionSet batchSet)
    {
        if (action == null)
            return;

        if (batchSet == null)
        {
            ApplyCameraSplineRule(action, selection.Index);
            return;
        }

        for (int i = 0; i < batchSet.Items.Count; i++)
            ApplyCameraSplineRule(action, batchSet.Items[i].Index);
    }

    /// <summary>重建指定 Camera Window 的预设路径，非法索引直接忽略。</summary>
    static void ApplyCameraSplineRule(ActionDefinition action, int index)
    {
        CameraShotNotifyState[] states = action.CameraShotStates;
        if (states == null || index < 0 || index >= states.Length || states[index] == null)
            return;

        CameraShotNotifyState shot = states[index];
        CameraSplineCurveRuleUtility.Apply(shot.PositionSpline, shot.SplineCurveRule);
    }

    /// <summary>只展示相机作者需要的 Spline 摘要与闭合开关，隐藏官方扩展数据字典。</summary>
    static void DrawCameraPositionSpline(
        ActionEditorSelectionSet batchSet,
        SerializedProperty element)
    {
        SerializedProperty spline = element.FindPropertyRelative("positionSpline");
        if (spline == null)
            return;

        SerializedProperty knots = spline.FindPropertyRelative("m_Knots");
        int knotCount = knots?.arraySize ?? 0;
        spline.isExpanded = EditorGUILayout.Foldout(
            spline.isExpanded,
            $"Position Spline ({knotCount} Knots)",
            true);
        if (!spline.isExpanded)
            return;

        EditorGUI.indentLevel++;
        SerializedProperty rule = element.FindPropertyRelative("splineCurveRule");
        bool custom = rule == null || rule.intValue == (int)CameraSplineCurveRule.Custom;
        using (new EditorGUI.DisabledScope(!custom))
        {
            DrawMultiProperty(
                batchSet,
                element,
                "positionSpline.m_Closed",
                new GUIContent("Closed", "仅 Custom 可闭合；预设规则固定为开放的首尾端点路径。"));
        }
        EditorGUILayout.HelpBox(
            "Knot、Rotation 与 Tangent 请在 Scene 视图中编辑；Spline 的 Int/Float/Object 扩展数据未被相机系统使用。",
            MessageType.None);
        EditorGUI.indentLevel--;
    }

    /// <summary>显式播放倍率；可选显示资源估测自然时长（只读，不驱动倍率）。</summary>
    static void DrawPlaybackSpeed(
        SerializedProperty element,
        ActionDefinition action,
        bool isVfx,
        ActionEditorSelectionSet batchSet)
    {
        SerializedProperty speedProp = element.FindPropertyRelative("playbackSpeed");
        if (speedProp != null)
        {
            bool mixed = IsMixed(batchSet, element, "playbackSpeed");
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.FloatField("Playback Speed", Mathf.Max(0.0001f, speedProp.floatValue));
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                speedProp.floatValue = Mathf.Max(0.0001f, value);
                PropagateRelative(batchSet, "playbackSpeed");
            }
        }

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

    /// <summary>绘制属性字段；变更时同步到同类型多选集合。</summary>
    static void DrawMultiProperty(
        ActionEditorSelectionSet batchSet,
        SerializedProperty element,
        string relativePath,
        GUIContent label = null,
        bool includeChildren = false)
    {
        SerializedProperty prop = element.FindPropertyRelative(relativePath);
        if (prop == null)
            return;

        bool mixed = IsMixed(batchSet, element, relativePath);
        EditorGUI.showMixedValue = mixed;
        EditorGUI.BeginChangeCheck();
        if (label != null)
            EditorGUILayout.PropertyField(prop, label, includeChildren);
        else
            EditorGUILayout.PropertyField(prop, includeChildren);
        EditorGUI.showMixedValue = false;

        if (EditorGUI.EndChangeCheck())
            PropagateRelative(batchSet, relativePath);
    }

    /// <summary>整型滑条；变更时同步到同类型多选集合。</summary>
    static void DrawMultiIntSlider(
        ActionEditorSelectionSet batchSet,
        SerializedProperty element,
        string relativePath,
        string label,
        int min,
        int max,
        bool clampMinToPrimaryStart = false)
    {
        SerializedProperty prop = element.FindPropertyRelative(relativePath);
        if (prop == null)
            return;

        bool mixed = IsMixed(batchSet, element, relativePath);
        EditorGUI.showMixedValue = mixed;
        EditorGUI.BeginChangeCheck();
        int value = EditorGUILayout.IntSlider(label, prop.intValue, min, max);
        EditorGUI.showMixedValue = false;
        if (!EditorGUI.EndChangeCheck())
            return;

        prop.intValue = value;
        PropagateRelative(batchSet, relativePath);

        // End Frame 批量写后，确保每个窗口 end >= 自己的 start
        if (clampMinToPrimaryStart && batchSet != null && relativePath == "endFrame")
        {
            for (int i = 0; i < batchSet.Items.Count; i++)
            {
                SerializedProperty peer = batchSet.Items[i].ElementProperty;
                if (peer == null)
                    continue;
                SerializedProperty peerStart = peer.FindPropertyRelative("startFrame");
                SerializedProperty peerEnd = peer.FindPropertyRelative("endFrame");
                if (peerStart != null && peerEnd != null)
                    peerEnd.intValue = Mathf.Max(peerEnd.intValue, peerStart.intValue);
            }
        }
    }

    /// <summary>把主选中项的相对属性深拷贝到其余同类型选中项。</summary>
    static void PropagateRelative(ActionEditorSelectionSet batchSet, string relativePath)
    {
        if (batchSet == null || batchSet.Count <= 1 || string.IsNullOrEmpty(relativePath))
            return;

        ActionEditorSelection primary = batchSet.Primary;
        SerializedProperty srcRoot = primary.ElementProperty;
        if (srcRoot == null)
            return;

        SerializedProperty src = srcRoot.FindPropertyRelative(relativePath);
        if (src == null)
            return;

        for (int i = 0; i < batchSet.Items.Count; i++)
        {
            ActionEditorSelection item = batchSet.Items[i];
            if (item.Equals(primary) || item.Kind != primary.Kind)
                continue;

            SerializedProperty dstRoot = item.ElementProperty;
            if (dstRoot == null)
                continue;

            SerializedProperty dst = dstRoot.FindPropertyRelative(relativePath);
            if (dst == null)
                continue;

            CopyPropertyRecursive(src, dst);
        }
    }

    /// <summary>多选间该相对路径取值是否不一致（用于 Mixed Value 显示）。</summary>
    static bool IsMixed(ActionEditorSelectionSet batchSet, SerializedProperty primaryElement, string relativePath)
    {
        if (batchSet == null || batchSet.Count <= 1 || primaryElement == null)
            return false;

        SerializedProperty primary = primaryElement.FindPropertyRelative(relativePath);
        if (primary == null)
            return false;

        for (int i = 0; i < batchSet.Items.Count; i++)
        {
            ActionEditorSelection item = batchSet.Items[i];
            if (item.Equals(batchSet.Primary))
                continue;

            SerializedProperty peerRoot = item.ElementProperty;
            SerializedProperty peer = peerRoot?.FindPropertyRelative(relativePath);
            if (peer == null)
                return true;

            if (!SerializedPropertiesEqual(primary, peer))
                return true;
        }

        return false;
    }

    /// <summary>选中项是否全部同 Kind。</summary>
    static bool AreAllSameKind(ActionEditorSelectionSet selectionSet)
    {
        if (selectionSet == null || selectionSet.Count <= 1)
            return true;

        ActionTimelineTrackKind kind = selectionSet.Primary.Kind;
        for (int i = 0; i < selectionSet.Items.Count; i++)
        {
            if (selectionSet.Items[i].Kind != kind)
                return false;
        }

        return true;
    }

    /// <summary>递归拷贝 SerializedProperty 值（含 Generic 子树与数组）。</summary>
    static void CopyPropertyRecursive(SerializedProperty source, SerializedProperty dest)
    {
        if (source == null || dest == null)
            return;

        if (source.isArray && source.propertyType != SerializedPropertyType.String)
        {
            dest.arraySize = source.arraySize;
            for (int i = 0; i < source.arraySize; i++)
                CopyPropertyRecursive(source.GetArrayElementAtIndex(i), dest.GetArrayElementAtIndex(i));
            return;
        }

        if (source.propertyType == SerializedPropertyType.Generic)
        {
            // 只遍历直接子字段，再递归进子树，避免 NextVisible 扁平化导致相对路径找不到
            SerializedProperty srcChild = source.Copy();
            SerializedProperty end = source.GetEndProperty();
            int parentDepth = source.depth;
            bool enterChildren = true;
            while (srcChild.NextVisible(enterChildren) && !SerializedProperty.EqualContents(srcChild, end))
            {
                enterChildren = false;
                if (srcChild.depth != parentDepth + 1)
                    continue;

                SerializedProperty dstChild = dest.FindPropertyRelative(srcChild.name);
                if (dstChild != null)
                    CopyPropertyRecursive(srcChild, dstChild);
            }

            return;
        }

        CopyLeafProperty(source, dest);
    }

    /// <summary>拷贝叶子属性值。</summary>
    static void CopyLeafProperty(SerializedProperty source, SerializedProperty dest)
    {
        if (source.propertyType != dest.propertyType)
            return;

        switch (source.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.ArraySize:
            case SerializedPropertyType.Character:
                dest.intValue = source.intValue;
                break;
            case SerializedPropertyType.Boolean:
                dest.boolValue = source.boolValue;
                break;
            case SerializedPropertyType.Float:
                dest.floatValue = source.floatValue;
                break;
            case SerializedPropertyType.String:
                dest.stringValue = source.stringValue;
                break;
            case SerializedPropertyType.ObjectReference:
                dest.objectReferenceValue = source.objectReferenceValue;
                break;
            case SerializedPropertyType.Enum:
                dest.enumValueIndex = source.enumValueIndex;
                break;
            case SerializedPropertyType.Vector2:
                dest.vector2Value = source.vector2Value;
                break;
            case SerializedPropertyType.Vector3:
                dest.vector3Value = source.vector3Value;
                break;
            case SerializedPropertyType.Vector4:
                dest.vector4Value = source.vector4Value;
                break;
            case SerializedPropertyType.Color:
                dest.colorValue = source.colorValue;
                break;
            case SerializedPropertyType.Rect:
                dest.rectValue = source.rectValue;
                break;
            case SerializedPropertyType.Bounds:
                dest.boundsValue = source.boundsValue;
                break;
            case SerializedPropertyType.Quaternion:
                dest.quaternionValue = source.quaternionValue;
                break;
            case SerializedPropertyType.AnimationCurve:
                dest.animationCurveValue = source.animationCurveValue;
                break;
            case SerializedPropertyType.ExposedReference:
                dest.exposedReferenceValue = source.exposedReferenceValue;
                break;
            case SerializedPropertyType.Vector2Int:
                dest.vector2IntValue = source.vector2IntValue;
                break;
            case SerializedPropertyType.Vector3Int:
                dest.vector3IntValue = source.vector3IntValue;
                break;
            case SerializedPropertyType.RectInt:
                dest.rectIntValue = source.rectIntValue;
                break;
            case SerializedPropertyType.BoundsInt:
                dest.boundsIntValue = source.boundsIntValue;
                break;
            case SerializedPropertyType.ManagedReference:
                dest.managedReferenceValue = source.managedReferenceValue;
                break;
        }
    }

    /// <summary>比较两属性值是否相等（含子树）。</summary>
    static bool SerializedPropertiesEqual(SerializedProperty a, SerializedProperty b)
    {
        if (a == null || b == null)
            return a == b;
        if (a.propertyType != b.propertyType)
            return false;

        if (a.isArray && a.propertyType != SerializedPropertyType.String)
        {
            if (a.arraySize != b.arraySize)
                return false;
            for (int i = 0; i < a.arraySize; i++)
            {
                if (!SerializedPropertiesEqual(a.GetArrayElementAtIndex(i), b.GetArrayElementAtIndex(i)))
                    return false;
            }

            return true;
        }

        if (a.propertyType == SerializedPropertyType.Generic)
        {
            SerializedProperty aChild = a.Copy();
            SerializedProperty end = a.GetEndProperty();
            int parentDepth = a.depth;
            bool enterChildren = true;
            while (aChild.NextVisible(enterChildren) && !SerializedProperty.EqualContents(aChild, end))
            {
                enterChildren = false;
                if (aChild.depth != parentDepth + 1)
                    continue;

                SerializedProperty bChild = b.FindPropertyRelative(aChild.name);
                if (bChild == null || !SerializedPropertiesEqual(aChild, bChild))
                    return false;
            }

            return true;
        }

        switch (a.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.ArraySize:
            case SerializedPropertyType.Character:
            case SerializedPropertyType.Enum:
                return a.intValue == b.intValue;
            case SerializedPropertyType.Boolean:
                return a.boolValue == b.boolValue;
            case SerializedPropertyType.Float:
                return Mathf.Approximately(a.floatValue, b.floatValue);
            case SerializedPropertyType.String:
                return a.stringValue == b.stringValue;
            case SerializedPropertyType.ObjectReference:
                return a.objectReferenceValue == b.objectReferenceValue;
            case SerializedPropertyType.Vector2:
                return a.vector2Value == b.vector2Value;
            case SerializedPropertyType.Vector3:
                return a.vector3Value == b.vector3Value;
            case SerializedPropertyType.Vector4:
                return a.vector4Value == b.vector4Value;
            case SerializedPropertyType.Color:
                return a.colorValue == b.colorValue;
            case SerializedPropertyType.Quaternion:
                return a.quaternionValue == b.quaternionValue;
            case SerializedPropertyType.Vector2Int:
                return a.vector2IntValue == b.vector2IntValue;
            case SerializedPropertyType.Vector3Int:
                return a.vector3IntValue == b.vector3IntValue;
            default:
                return false;
        }
    }
}

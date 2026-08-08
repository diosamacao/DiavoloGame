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

using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ActionDefinition Inspector：战斗预览（Pose Scrub / Hitbox / VFX）与 Baked Motion 烘焙入口。
/// </summary>
[CustomEditor(typeof(ActionDefinition))]
public class ActionDefinitionHitboxEditor : Editor
{
    const string PreviewCharacterPrefKey = "ACTGame.ActionEditorPreview.PreviewCharacter";
    const string PreviewFramePrefKeyPrefix = "ACTGame.ActionEditorPreview.Frame.";
    const string PreviewHitboxEnabledPrefKey = "ACTGame.ActionEditorPreview.ShowHitbox";
    const string PreviewVfxEnabledPrefKey = "ACTGame.ActionEditorPreview.ShowVfx";
    const string PreviewTrajectoryEnabledPrefKey = "ACTGame.ActionEditorPreview.ShowTrajectory";
    const string RootMotionFolderPrefKey = "ACTGame.MotionBake.RootMotionFolder";

    SerializedProperty _hitboxStatesProp;
    SerializedProperty _playVfxNotifiesProp;

    ActionEditorPreviewSession _previewSession;
    ActionEditorVfxPreviewExtension _vfxPreviewExtension;

    Transform _previewCharacter;
    int _previewFrame;
    int _selectedHitboxIndex;
    int _selectedVfxIndex;
    bool _previewHitboxEnabled;
    bool _previewVfxEnabled;
    bool _previewTrajectoryEnabled;
    DefaultAsset _rootMotionFolder;
    // 直线连击默认 ForwardSigned，避免把横摆 bake 进逻辑根；侧闪类招式请改 FullPlanar
    ActionMotionPlanarMode _motionPlanarMode = ActionMotionPlanarMode.ForwardSigned;

    bool _editorUpdateHooked;
    bool _hasMotionDirtyCache;
    bool _cachedMotionDirty;
    string _cachedMotionDirtyFolder;
    int _cachedMotionDirtyCount = -1;
    int _cachedNaturalDurationPrefabId;
    float _cachedNaturalDurationSeconds;

    void OnEnable()
    {
        SerializedProperty timelineProp = serializedObject.FindProperty("timeline");
        _hitboxStatesProp = timelineProp?.FindPropertyRelative("hitboxStates");
        _playVfxNotifiesProp = timelineProp?.FindPropertyRelative("playVfxNotifies");

        _vfxPreviewExtension = new ActionEditorVfxPreviewExtension();
        _vfxPreviewExtension.Bind(GetVfxArrayProperty);

        _previewSession = new ActionEditorPreviewSession(this);
        _previewSession.RegisterExtension(_vfxPreviewExtension);
        _previewSession.SetAction((ActionDefinition)target);

        SceneView.duringSceneGui += OnSceneGUI;

        RestorePreviewCharacter();
        RestorePreviewFrame();
        RestorePreviewToggles();
        SyncEditorUpdateHook();

        string rmPath = EditorPrefs.GetString(RootMotionFolderPrefKey, "Assets/Art/Arts/Unagi/RootMotion");
        _rootMotionFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(rmPath);
        InvalidateMotionDirtyCache();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SetEditorUpdateHooked(false);

        SavePreviewCharacter();
        SavePreviewFrame();
        SavePreviewToggles();

        _previewSession?.Dispose();
        _previewSession = null;
    }

    /// <summary>同步 Session 状态并驱动动画采样与各 PreviewExtension。</summary>
    void OnEditorUpdate()
    {
        if (_previewSession == null || _previewCharacter == null)
            return;

        _previewSession.SetAction((ActionDefinition)target);
        _previewSession.SetPreviewCharacter(_previewCharacter);
        _previewSession.SetPreviewFrame(_previewFrame);
        _vfxPreviewExtension.IsEnabled = _previewVfxEnabled;
        _previewSession.Tick();
    }

    /// <summary>仅在绑定 Preview Character 时挂 EditorApplication.update，避免无预览时空转。</summary>
    void SyncEditorUpdateHook() => SetEditorUpdateHooked(_previewCharacter != null);

    void SetEditorUpdateHooked(bool hooked)
    {
        if (_editorUpdateHooked == hooked)
            return;

        if (hooked)
            EditorApplication.update += OnEditorUpdate;
        else
            EditorApplication.update -= OnEditorUpdate;

        _editorUpdateHooked = hooked;
    }

    /// <summary>缓存 Dirty 查询；资产 DirtyCount 或 RM 文件夹变化时失效。</summary>
    bool ResolveMotionDirty(ActionDefinition action, string rootMotionFolder)
    {
        if (action == null || !AssetDatabase.IsValidFolder(rootMotionFolder))
            return false;

        int dirtyCount = EditorUtility.GetDirtyCount(action);
        if (_hasMotionDirtyCache
            && _cachedMotionDirtyCount == dirtyCount
            && string.Equals(_cachedMotionDirtyFolder, rootMotionFolder, StringComparison.Ordinal))
            return _cachedMotionDirty;

        _cachedMotionDirty = ActionMotionDirtyUtility.IsDirty(action, rootMotionFolder, ActionSim.LogicHz);
        _cachedMotionDirtyCount = dirtyCount;
        _cachedMotionDirtyFolder = rootMotionFolder;
        _hasMotionDirtyCache = true;
        return _cachedMotionDirty;
    }

    void InvalidateMotionDirtyCache()
    {
        _hasMotionDirtyCache = false;
        _cachedMotionDirtyCount = -1;
        _cachedMotionDirtyFolder = null;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        ActionDefinition action = (ActionDefinition)target;
        int maxFrame = Mathf.Max(0, action.TotalFrames - 1);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("战斗预览", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        _previewCharacter = (Transform)EditorGUILayout.ObjectField(
            "Preview Character",
            _previewCharacter,
            typeof(Transform),
            true);

        _previewFrame = EditorGUILayout.IntSlider("Preview Frame", _previewFrame, 0, maxFrame);

        EditorGUILayout.Space(4f);
        _previewHitboxEnabled = EditorGUILayout.Toggle("Show Hitbox Preview", _previewHitboxEnabled);
        _previewVfxEnabled = EditorGUILayout.Toggle("Show VFX Preview", _previewVfxEnabled);
        _previewTrajectoryEnabled = EditorGUILayout.Toggle(
            "Show Baked Trajectory (Wave0)",
            _previewTrajectoryEnabled);

        if (EditorGUI.EndChangeCheck())
        {
            SavePreviewCharacter();
            SavePreviewFrame();
            SavePreviewToggles();
            SyncEditorUpdateHook();
            if (!_previewVfxEnabled)
                _vfxPreviewExtension?.SetEnabled(false);
            SceneView.RepaintAll();
        }

        DrawAnimationPreviewHints(action);
        DrawHitboxPreviewSection(maxFrame);
        DrawVfxPreviewSection(maxFrame);
        DrawBakedMotionSection(action);

        if (_previewCharacter == null)
        {
            EditorGUILayout.HelpBox(
                "拖入场景中的 Player Transform 以在 Scene 视图预览完整招式表现（动画 + Hitbox + VFX）。",
                MessageType.Warning);
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>只读展示运动表，并提供按 RootMotion 文件夹烘焙当前招。</summary>
    void DrawBakedMotionSection(ActionDefinition action)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Baked Motion", EditorStyles.boldLabel);

        ActionBakedMotion motion = action.BakedMotion;
        string rmPathForDirty = _rootMotionFolder != null
            ? AssetDatabase.GetAssetPath(_rootMotionFolder)
            : EditorPrefs.GetString(RootMotionFolderPrefKey, string.Empty);
        // Dirty 结果按资产 DirtyCount + 文件夹缓存，避免滚动 Inspector 时反复扫 RM 目录。
        bool dirty = ResolveMotionDirty(action, rmPathForDirty);
        if (dirty)
        {
            EditorGUILayout.HelpBox(
                "运动表 Dirty：InPlace/RM hash、logicHz 或段帧窗口与烘焙结果不一致。请 Bake Motion 或文件夹 Bake Dirty。",
                MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.EnumPopup("Status", motion.bakeStatus);
            EditorGUILayout.IntField("Frame Count", motion.frameCount);
            EditorGUILayout.TextField("Matched RM", motion.matchedRootMotionName ?? string.Empty);
            EditorGUILayout.Toggle("Dirty", dirty);
        }

        EditorGUI.BeginChangeCheck();
        _rootMotionFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "RootMotion Folder",
            _rootMotionFolder,
            typeof(DefaultAsset),
            false);
        if (EditorGUI.EndChangeCheck())
            InvalidateMotionDirtyCache();

        _motionPlanarMode = (ActionMotionPlanarMode)EditorGUILayout.EnumPopup(
            "Planar Mode",
            _motionPlanarMode);
        EditorGUILayout.HelpBox(
            "Wave1：直线连击用 ForwardSigned（丢弃横摆进逻辑根）；侧闪/横移斩用 FullPlanar；"
            + "ForwardOnly 为旧保模长语义勿再用于新烘焙。只烘焙水平位移；朝向不读运动表 yaw。",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Bake Motion"))
            {
                string rm = _rootMotionFolder != null
                    ? AssetDatabase.GetAssetPath(_rootMotionFolder)
                    : string.Empty;
                if (!AssetDatabase.IsValidFolder(rm))
                {
                    EditorUtility.DisplayDialog("Bake Motion", "请指定有效 RootMotion 文件夹。", "OK");
                }
                else
                {
                    EditorPrefs.SetString(RootMotionFolderPrefKey, rm);
                    bool ok = ActionMotionBakeService.BakeAction(
                        action,
                        rm,
                        _motionPlanarMode,
                        ActionSim.LogicHz,
                        out string message);
                    InvalidateMotionDirtyCache();
                    EditorUtility.DisplayDialog(
                        ok ? "Bake Motion OK" : "Bake Motion Failed",
                        message,
                        "OK");
                }
            }

            if (GUILayout.Button("Open Folder Bake Window"))
                FolderMotionBakeWindow.Open();
        }
    }

    void DrawAnimationPreviewHints(ActionDefinition action)
    {
        if (_previewCharacter == null)
            return;

        if (!action.HasAnimation)
        {
            EditorGUILayout.HelpBox("该 ActionDefinition 未配置 Animation Segments，无法预览角色动作。", MessageType.Warning);
            return;
        }

        if (!ActionEditorAnimationSampler.TryResolveSampleRoot(_previewCharacter, out _, out _))
        {
            EditorGUILayout.HelpBox(
                "Preview Character 上未找到 Animator。",
                MessageType.Warning);
        }
    }

    void DrawHitboxPreviewSection(int maxFrame)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Hitbox 预览", EditorStyles.boldLabel);

        if (!_previewHitboxEnabled)
        {
            EditorGUILayout.HelpBox("已关闭 Hitbox Scene 预览；勾选 Show Hitbox Preview 以显示线框与 Handles。", MessageType.None);
            return;
        }

        int hitboxCount = _hitboxStatesProp != null ? _hitboxStatesProp.arraySize : 0;
        if (hitboxCount > 0)
        {
            EditorGUILayout.HelpBox(
                "Hitbox 线框按 Preview Frame 高亮当前激活窗口；Selected 仅用于 Scene Handles 编辑。",
                MessageType.None);
            _selectedHitboxIndex = EditorGUILayout.IntSlider(
                "Selected Hitbox (Handles)",
                Mathf.Clamp(_selectedHitboxIndex, 0, hitboxCount - 1),
                0,
                hitboxCount - 1);
        }
        else
        {
            EditorGUILayout.HelpBox("在 Timeline / Hitbox States 列表中添加至少一条 HitboxNotifyState。", MessageType.Info);
        }
    }

    void DrawVfxPreviewSection(int maxFrame)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("VFX 预览", EditorStyles.boldLabel);

        if (!_previewVfxEnabled)
        {
            EditorGUILayout.HelpBox("已关闭 VFX Scene 预览；勾选 Show VFX Preview 以显示刀光 Prefab 与 Handles。", MessageType.None);
            return;
        }

        int vfxCount = _playVfxNotifiesProp != null ? _playVfxNotifiesProp.arraySize : 0;
        if (vfxCount > 0)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.HelpBox(
                "Scene Prefab/粒子按 Preview Frame 自动显示全部已触发 VFX，无需选中条目；下方 Selected 仅用于 Handles 编辑。",
                MessageType.None);

            _selectedVfxIndex = EditorGUILayout.IntSlider(
                "Selected VFX (Handles)",
                Mathf.Clamp(_selectedVfxIndex, 0, vfxCount - 1),
                0,
                vfxCount - 1);

            SerializedProperty selectedProp = _playVfxNotifiesProp.GetArrayElementAtIndex(_selectedVfxIndex);
            SerializedProperty startFrameProp = selectedProp.FindPropertyRelative("startFrame");
            SerializedProperty endFrameProp = selectedProp.FindPropertyRelative("endFrame");
            SerializedProperty speedProp = selectedProp.FindPropertyRelative("playbackSpeed");
            SerializedProperty attachProp = selectedProp.FindPropertyRelative("attachPointId");

            if (startFrameProp != null)
            {
                startFrameProp.intValue = EditorGUILayout.IntSlider(
                    "Trigger Frame",
                    startFrameProp.intValue,
                    0,
                    maxFrame);
                if (endFrameProp != null)
                    endFrameProp.intValue = startFrameProp.intValue;
            }

            if (attachProp != null)
                EditorGUILayout.PropertyField(attachProp, new GUIContent("Attach Point Id"));

            if (speedProp != null)
                speedProp.floatValue = EditorGUILayout.FloatField("Playback Speed", Mathf.Max(0.0001f, speedProp.floatValue));

            GameObject prefab = selectedProp.FindPropertyRelative("prefab")?.objectReferenceValue as GameObject;
            if (prefab != null)
            {
                int prefabId = prefab.GetInstanceID();
                if (_cachedNaturalDurationPrefabId != prefabId)
                {
                    _cachedNaturalDurationPrefabId = prefabId;
                    _cachedNaturalDurationSeconds = ActionVfxPlayback.EstimateNaturalDurationSeconds(prefab);
                }

                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.FloatField("Estimated Duration (s)", _cachedNaturalDurationSeconds);
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                SceneView.RepaintAll();
            }

            if (prefab == null)
            {
                EditorGUILayout.HelpBox("为选中的 VFX 指定 Prefab 后可用 Handles 调整挂点偏移。", MessageType.Info);
            }
            else if (ActionVfxEditorPreview.HasParticleSystems(prefab))
            {
                EditorGUILayout.HelpBox(
                    "含 ParticleSystem：Scene 需开启 Effects；拖动 Preview Frame 到触发帧及之后即可预览。",
                    MessageType.Info);

                if (GUILayout.Button("Replay VFX Preview"))
                    _vfxPreviewExtension?.Replay();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("在 Timeline / Play Vfx Notifies 列表中添加至少一条 VFX 点事件。", MessageType.Info);
        }
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (target is not ActionDefinition action)
            return;

        // Wave 0：烘焙累计轨迹（橙=原始 Δ 累计，青=planarMode 生效后）
        if (_previewTrajectoryEnabled && _previewCharacter != null)
            ActionMotionTrajectorySceneDrawing.DrawBakedTrajectories(action, _previewCharacter);

        if (_previewCharacter == null)
            return;

        Transform root = _previewCharacter;

        if (_previewHitboxEnabled)
        {
            DrawAllHitboxPreviews(action, root);

            if (_hitboxStatesProp != null && _hitboxStatesProp.arraySize > 0)
            {
                int hitboxIndex = Mathf.Clamp(_selectedHitboxIndex, 0, _hitboxStatesProp.arraySize - 1);
                SerializedProperty hitboxProp = _hitboxStatesProp.GetArrayElementAtIndex(hitboxIndex);
                string attachId = hitboxProp.FindPropertyRelative("attachPointId")?.stringValue;
                Transform hitboxAnchor = ActionEditorPreviewAttachPoint.Resolve(root, attachId);
                DrawSelectedHitboxHandles(hitboxProp, hitboxAnchor);
            }
        }

        if (_previewVfxEnabled)
        {
            DrawAllVfxPreviews(action, root);

            if (_playVfxNotifiesProp != null && _playVfxNotifiesProp.arraySize > 0)
            {
                int vfxIndex = Mathf.Clamp(_selectedVfxIndex, 0, _playVfxNotifiesProp.arraySize - 1);
                SerializedProperty vfxProp = _playVfxNotifiesProp.GetArrayElementAtIndex(vfxIndex);
                string attachId = vfxProp.FindPropertyRelative("attachPointId")?.stringValue;
                Transform vfxAnchor = ActionEditorPreviewAttachPoint.Resolve(root, attachId);
                DrawSelectedVfxHandles(vfxProp, vfxAnchor);
            }
        }
    }

    /// <summary>提供全部 VFX 数组，供预览扩展按 Preview Frame 驱动（无需选中条目）。</summary>
    SerializedProperty GetVfxArrayProperty() => _playVfxNotifiesProp;

    /// <summary>绘制全部 Hitbox 线框：按各自 attachPointId 解析挂点。</summary>
    void DrawAllHitboxPreviews(ActionDefinition action, Transform root)
    {
        ActionFrameQueryResult frameQuery = ActionFrameQuery.Query(action, _previewFrame);
        HitboxNotifyState[] hitboxes = action.HitboxStates;
        for (int i = 0; i < hitboxes.Length; i++)
        {
            HitboxNotifyState hitbox = hitboxes[i];
            if (hitbox == null)
                continue;

            Transform anchor = ActionEditorPreviewAttachPoint.Resolve(root, hitbox.AttachPointId);
            bool isActive = frameQuery.IsStateActive(hitbox);
            bool isSelected = i == _selectedHitboxIndex;
            Color color = isSelected
                ? new Color(1f, 0.85f, 0.1f, 1f)
                : isActive
                    ? new Color(1f, 0.35f, 0.15f, 0.95f)
                    : new Color(0.6f, 0.6f, 0.6f, 0.35f);

            HitboxOrientedBox box = HitboxMath.BuildFromHitbox(root, anchor, hitbox);
            HitboxSceneDrawing.DrawWireOrientedBox(box, color);
        }
    }

    /// <summary>绘制全部 VFX 标记：触发帧后高亮，选中项青色。</summary>
    void DrawAllVfxPreviews(ActionDefinition action, Transform root)
    {
        PlayVfxNotify[] playVfxNotifies = action.PlayVfxNotifies;
        for (int i = 0; i < playVfxNotifies.Length; i++)
        {
            PlayVfxNotify vfxEvent = playVfxNotifies[i];
            if (vfxEvent == null)
                continue;

            Transform anchor = ActionEditorPreviewAttachPoint.Resolve(root, vfxEvent.AttachPointId);
            bool isActive = _previewFrame >= vfxEvent.TriggerFrame;
            bool isSelected = i == _selectedVfxIndex;
            Color color = isSelected
                ? new Color(0.2f, 0.95f, 1f, 1f)
                : isActive
                    ? new Color(0.35f, 0.75f, 1f, 0.95f)
                    : new Color(0.5f, 0.5f, 0.55f, 0.4f);

            ActionVfxSceneDrawing.DrawVfxMarker(anchor, vfxEvent, color);
        }
    }

    /// <summary>为选中的 Hitbox 绘制 Position / Rotation / Scale Handles。</summary>
    void DrawSelectedHitboxHandles(SerializedProperty hitboxProp, Transform anchor)
    {
        SerializedProperty offsetProp = hitboxProp.FindPropertyRelative("localOffset");
        SerializedProperty eulerProp = hitboxProp.FindPropertyRelative("localEulerAngles");
        SerializedProperty sizeProp = hitboxProp.FindPropertyRelative("size");

        if (offsetProp == null || eulerProp == null || sizeProp == null)
            return;

        Quaternion localRotation = Quaternion.Euler(eulerProp.vector3Value);
        Vector3 worldCenter = anchor.TransformPoint(offsetProp.vector3Value);
        Quaternion worldRotation = anchor.rotation * localRotation;
        float handleSize = HandleUtility.GetHandleSize(worldCenter);

        EditorGUI.BeginChangeCheck();

        Quaternion newWorldRotation = Handles.RotationHandle(worldRotation, worldCenter);
        Vector3 newWorldCenter = Handles.PositionHandle(worldCenter, newWorldRotation);
        Vector3 newSize = Handles.ScaleHandle(
            sizeProp.vector3Value,
            newWorldCenter,
            newWorldRotation,
            handleSize);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(target, "Edit Hitbox");

            offsetProp.vector3Value = anchor.InverseTransformPoint(newWorldCenter);
            eulerProp.vector3Value = (Quaternion.Inverse(anchor.rotation) * newWorldRotation).eulerAngles;
            sizeProp.vector3Value = Vector3.Max(newSize, Vector3.one * 0.01f);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }

    /// <summary>为选中的 VFX 绘制 Position / Rotation / Scale Handles。</summary>
    void DrawSelectedVfxHandles(SerializedProperty vfxProp, Transform anchor)
    {
        SerializedProperty offsetProp = vfxProp.FindPropertyRelative("localOffset");
        SerializedProperty eulerProp = vfxProp.FindPropertyRelative("localEulerAngles");
        SerializedProperty scaleProp = vfxProp.FindPropertyRelative("localScale");

        if (offsetProp == null || eulerProp == null || scaleProp == null)
            return;

        Quaternion localRotation = Quaternion.Euler(eulerProp.vector3Value);
        Vector3 worldCenter = anchor.TransformPoint(offsetProp.vector3Value);
        Quaternion worldRotation = anchor.rotation * localRotation;
        float handleSize = HandleUtility.GetHandleSize(worldCenter);

        EditorGUI.BeginChangeCheck();

        Quaternion newWorldRotation = Handles.RotationHandle(worldRotation, worldCenter);
        Vector3 newWorldCenter = Handles.PositionHandle(worldCenter, newWorldRotation);
        Vector3 newScale = Handles.ScaleHandle(
            scaleProp.vector3Value,
            newWorldCenter,
            newWorldRotation,
            handleSize);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(target, "Edit VFX");

            offsetProp.vector3Value = anchor.InverseTransformPoint(newWorldCenter);
            eulerProp.vector3Value = (Quaternion.Inverse(anchor.rotation) * newWorldRotation).eulerAngles;
            scaleProp.vector3Value = Vector3.Max(newScale, Vector3.one * 0.01f);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
        }
    }

    void SavePreviewCharacter()
    {
        if (_previewCharacter == null)
        {
            EditorPrefs.DeleteKey(PreviewCharacterPrefKey);
            return;
        }

        GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(_previewCharacter);
        EditorPrefs.SetString(PreviewCharacterPrefKey, id.ToString());
    }

    void RestorePreviewCharacter()
    {
        if (!EditorPrefs.HasKey(PreviewCharacterPrefKey))
            return;

        string idString = EditorPrefs.GetString(PreviewCharacterPrefKey);
        if (!GlobalObjectId.TryParse(idString, out GlobalObjectId globalId))
            return;

        UnityEngine.Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
        _previewCharacter = obj as Transform;
    }

    void SavePreviewFrame()
    {
        if (target == null)
            return;

        EditorPrefs.SetInt(PreviewFramePrefKeyPrefix + target.GetInstanceID(), _previewFrame);
    }

    void RestorePreviewFrame()
    {
        if (target == null)
            return;

        string key = PreviewFramePrefKeyPrefix + target.GetInstanceID();
        if (EditorPrefs.HasKey(key))
            _previewFrame = EditorPrefs.GetInt(key);
    }

    void SavePreviewToggles()
    {
        EditorPrefs.SetBool(PreviewHitboxEnabledPrefKey, _previewHitboxEnabled);
        EditorPrefs.SetBool(PreviewVfxEnabledPrefKey, _previewVfxEnabled);
        EditorPrefs.SetBool(PreviewTrajectoryEnabledPrefKey, _previewTrajectoryEnabled);
    }

    void RestorePreviewToggles()
    {
        _previewHitboxEnabled = EditorPrefs.GetBool(PreviewHitboxEnabledPrefKey, true);
        _previewVfxEnabled = EditorPrefs.GetBool(PreviewVfxEnabledPrefKey, false);
        // 轨迹折线按帧累计绘制，默认关闭以免打开 Inspector 时 Scene 额外开销。
        _previewTrajectoryEnabled = EditorPrefs.GetBool(PreviewTrajectoryEnabledPrefKey, false);
    }
}

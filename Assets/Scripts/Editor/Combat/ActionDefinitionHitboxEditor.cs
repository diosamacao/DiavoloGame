using UnityEditor;
using UnityEngine;

/// <summary>
/// ActionDefinition 战斗预览编辑器：动画 Pose 帧 Scrub + Hitbox / VFX Scene Handles。
/// 预览管线由 ActionEditorPreviewSession 驱动，扩展点见 IActionEditorPreviewExtension。
/// </summary>
[CustomEditor(typeof(ActionDefinition))]
public class ActionDefinitionHitboxEditor : Editor
{
    const string PreviewCharacterPrefKey = "ACTGame.ActionEditorPreview.PreviewCharacter";
    const string PreviewFramePrefKeyPrefix = "ACTGame.ActionEditorPreview.Frame.";
    const string PreviewHitboxEnabledPrefKey = "ACTGame.ActionEditorPreview.ShowHitbox";
    const string PreviewVfxEnabledPrefKey = "ACTGame.ActionEditorPreview.ShowVfx";

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

    void OnEnable()
    {
        SerializedProperty timelineProp = serializedObject.FindProperty("timeline");
        _hitboxStatesProp = timelineProp?.FindPropertyRelative("hitboxStates");
        _playVfxNotifiesProp = timelineProp?.FindPropertyRelative("playVfxNotifies");

        _vfxPreviewExtension = new ActionEditorVfxPreviewExtension();
        _vfxPreviewExtension.Bind(GetSelectedVfxProperty);

        _previewSession = new ActionEditorPreviewSession(this);
        _previewSession.RegisterExtension(_vfxPreviewExtension);
        _previewSession.SetAction((ActionDefinition)target);

        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += OnEditorUpdate;

        RestorePreviewCharacter();
        RestorePreviewFrame();
        RestorePreviewToggles();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= OnEditorUpdate;

        SavePreviewCharacter();
        SavePreviewFrame();
        SavePreviewToggles();

        _previewSession?.Dispose();
        _previewSession = null;
    }

    /// <summary>同步 Session 状态并驱动动画采样与各 PreviewExtension。</summary>
    void OnEditorUpdate()
    {
        if (_previewSession == null)
            return;

        _previewSession.SetAction((ActionDefinition)target);
        _previewSession.SetPreviewCharacter(_previewCharacter);
        _previewSession.SetPreviewFrame(_previewFrame);
        _vfxPreviewExtension.IsEnabled = _previewVfxEnabled;
        _previewSession.Tick();
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

        if (EditorGUI.EndChangeCheck())
        {
            SavePreviewCharacter();
            SavePreviewFrame();
            SavePreviewToggles();
            if (!_previewVfxEnabled)
                _vfxPreviewExtension?.SetEnabled(false);
            SceneView.RepaintAll();
        }

        DrawAnimationPreviewHints(action);
        DrawHitboxPreviewSection(maxFrame);
        DrawVfxPreviewSection(maxFrame);

        if (_previewCharacter == null)
        {
            EditorGUILayout.HelpBox(
                "拖入场景中的 Player Transform 以在 Scene 视图预览完整招式表现（动画 + Hitbox + VFX）。",
                MessageType.Warning);
        }

        serializedObject.ApplyModifiedProperties();
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
            _selectedHitboxIndex = EditorGUILayout.IntSlider(
                "Selected Hitbox",
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
            _selectedVfxIndex = EditorGUILayout.IntSlider(
                "Selected VFX",
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
                float natural = ActionVfxPlayback.EstimateNaturalDurationSeconds(prefab);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.FloatField("Estimated Duration (s)", natural);
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                SceneView.RepaintAll();
            }

            if (prefab == null)
            {
                EditorGUILayout.HelpBox("为选中的 VFX 指定 Prefab 后可在 Scene 中预览刀光位置。", MessageType.Info);
            }
            else if (ActionVfxEditorPreview.HasParticleSystems(prefab))
            {
                EditorGUILayout.HelpBox(
                    "该 Prefab 含 ParticleSystem：Scene 视图需开启 Effects（Gizmo 菜单），拖动时间轴可按 Playback Speed 逐帧预览。",
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
        if (target is not ActionDefinition action || _previewCharacter == null)
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

    SerializedProperty GetSelectedVfxProperty()
    {
        if (_playVfxNotifiesProp == null || _playVfxNotifiesProp.arraySize == 0)
            return null;

        int vfxIndex = Mathf.Clamp(_selectedVfxIndex, 0, _playVfxNotifiesProp.arraySize - 1);
        return _playVfxNotifiesProp.GetArrayElementAtIndex(vfxIndex);
    }

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

        Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
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
    }

    void RestorePreviewToggles()
    {
        _previewHitboxEnabled = EditorPrefs.GetBool(PreviewHitboxEnabledPrefKey, true);
        _previewVfxEnabled = EditorPrefs.GetBool(PreviewVfxEnabledPrefKey, false);
    }
}

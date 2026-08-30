using System;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>Action Editor 的相机样条预览：直接编辑 Knot/Tangent，并复用运行时 Pose 求值。</summary>
public sealed class ActionEditorCameraShotPreview : IActionEditorPreviewExtension
{
    /// <summary>Scene 构图写回时如何处理当前预览帧的 FOV Key。</summary>
    enum SceneCaptureFovMode
    {
        KeepShot = 0,
        SceneView = 1,
        Custom = 2,
    }

    /// <summary>临时采样动作帧的挂点世界 Pose；只用于 Snapshot Binding 入窗缓存。</summary>
    public delegate bool TryEvaluateWorldPose(
        int frame,
        string attachPointId,
        Vector3 localOffset,
        Vector3 localEuler,
        out Vector3 worldPosition,
        out Quaternion worldRotation);

    const int PathSamples = 48;
    const int KnotControlHint = 0x43A710;

    Func<ActionEditorSelection> _selectionGetter;
    Func<SerializedObject> _serializedObjectGetter;
    TryEvaluateWorldPose _worldPoseEvaluator;
    ActionEditorPreviewContext _context;
    bool _hasContext;
    Transform _previewTarget;
    CameraShotNotifyState _selectedShot;
    int _selectedKnot = -1;
    readonly Vector3[] _pathPoints = new Vector3[PathSamples + 1];
    CameraShotNotifyState _snapshotShot;
    int _snapshotBindingHash;
    CameraReferencePose _referenceSnapshot;
    CameraReferencePose _lookAtSnapshot;
    bool _hasReferenceSnapshot;
    bool _hasLookAtSnapshot;
    SceneCaptureFovMode _sceneCaptureFovMode = SceneCaptureFovMode.SceneView;
    float _customCaptureFov = 60f;

    /// <summary>绑定当前选中窗口与 Action SerializedObject；Spline 数据通过目标对象写入。</summary>
    public void Bind(
        Func<ActionEditorSelection> selectionGetter,
        Func<SerializedObject> serializedObjectGetter,
        TryEvaluateWorldPose worldPoseEvaluator)
    {
        _selectionGetter = selectionGetter;
        _serializedObjectGetter = serializedObjectGetter;
        _worldPoseEvaluator = worldPoseEvaluator;
    }

    /// <inheritdoc />
    public void OnPreviewBegin(in ActionEditorPreviewContext context)
    {
        _context = context;
        _hasContext = context.IsValid;
        _selectedShot = null;
        _selectedKnot = -1;
        InvalidateSnapshotCache();
    }

    /// <inheritdoc />
    public void OnPreviewUpdate(in ActionEditorPreviewContext context)
    {
        _context = context;
        _hasContext = context.IsValid;
        PublishCameraViewPose();
    }

    /// <inheritdoc />
    public void OnPreviewEnd(in ActionEditorPreviewContext context)
    {
        _hasContext = false;
        ActionEditorCameraView.ClearPose();
        _selectedShot = null;
        _selectedKnot = -1;
        InvalidateSnapshotCache();
        if (_previewTarget != null)
            UnityEngine.Object.DestroyImmediate(_previewTarget.gameObject);
        _previewTarget = null;
    }

    /// <summary>预览 Tick 独立推送专用 Camera View，使其不依赖 SceneView 是否正在重绘。</summary>
    void PublishCameraViewPose()
    {
        if (!_hasContext || _context.Action == null || _context.PreviewCharacter == null)
        {
            ActionEditorCameraView.ClearPose();
            return;
        }

        CameraShotNotifyState shot =
            _context.Action.GetActiveCameraShotAtFrame(_context.PreviewFrame);
        if (shot == null)
        {
            ActionEditorCameraView.ClearPose();
            return;
        }

        Transform previewTarget = EnsurePreviewTarget(_context.PreviewCharacter);
        CameraAnchorProvider characterProvider = ResolveProvider(_context.PreviewCharacter);
        EnsureSnapshotCache(shot, characterProvider);
        if (!TryResolvePreviewBinding(
                shot.ReferenceBinding,
                true,
                previewTarget,
                characterProvider,
                out CameraReferencePose referencePose)
            || !TryResolvePreviewBinding(
                shot.LookAtBinding,
                false,
                previewTarget,
                characterProvider,
                out CameraReferencePose lookAtPose)
            || !CameraShotPoseResolver.TryResolvePose(
                shot,
                referencePose,
                lookAtPose,
                _context.PreviewFrame,
                out CameraShotPose pose))
        {
            ActionEditorCameraView.ClearPose();
            return;
        }

        ActionEditorCameraView.Publish(shot.Id, _context.PreviewFrame, pose);
    }

    /// <summary>绘制当前 Shot 的样条、选中 Knot 手柄及其按需视锥；不恢复常驻机位 Debug。</summary>
    public void DrawSceneGUI(SceneView sceneView)
    {
        if (!_hasContext || _context.Action == null || _context.PreviewCharacter == null)
        {
            ActionEditorCameraView.ClearPose();
            return;
        }

        CameraShotNotifyState shot =
            _context.Action.GetActiveCameraShotAtFrame(_context.PreviewFrame);
        if (shot == null)
        {
            ActionEditorCameraView.ClearPose();
            return;
        }
        if (!ReferenceEquals(_selectedShot, shot))
        {
            _selectedShot = shot;
            _selectedKnot = -1;
        }

        Transform previewTarget = EnsurePreviewTarget(_context.PreviewCharacter);
        CameraAnchorProvider characterProvider = ResolveProvider(_context.PreviewCharacter);
        EnsureSnapshotCache(shot, characterProvider);
        if (!TryResolvePreviewBinding(
                shot.ReferenceBinding,
                true,
                previewTarget,
                characterProvider,
                out CameraReferencePose referencePose)
            || !TryResolvePreviewBinding(
                shot.LookAtBinding,
                false,
                previewTarget,
                characterProvider,
                out CameraReferencePose lookAtPose))
        {
            ActionEditorCameraView.ClearPose();
            DrawBindingError();
            return;
        }

        DrawSplinePath(shot.PositionSpline, referencePose);
        if (IsCameraWindowSelected())
        {
            HandleToolShortcuts(shot.SplineCurveRule == CameraSplineCurveRule.Custom);
            DrawSplineHandles(shot.PositionSpline, referencePose, shot.SplineCurveRule);
        }

        if (CameraShotPoseResolver.TryResolvePose(
                shot,
                referencePose,
                lookAtPose,
                _context.PreviewFrame,
                out CameraShotPose pose))
        {
            ActionEditorCameraView.Publish(shot.Id, _context.PreviewFrame, pose);
            if (IsCameraWindowSelected())
                DrawCurrentFrameFrustum(sceneView, pose);
            DrawSceneOverlay(sceneView, shot, referencePose, lookAtPose, pose);
        }
        else
        {
            ActionEditorCameraView.ClearPose();
        }
    }

    /// <summary>Scene 预览没有真实 SelectedTarget 时，在角色前方提供临时目标 Root。</summary>
    Transform EnsurePreviewTarget(Transform previewCharacter)
    {
        if (_previewTarget == null)
        {
            var go = new GameObject("ActionEditorCameraTarget")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            _previewTarget = go.transform;
        }

        _previewTarget.SetPositionAndRotation(
            previewCharacter.position + previewCharacter.forward * 3f,
            previewCharacter.rotation);
        return _previewTarget;
    }

    /// <summary>从 Preview Character 向上或向下解析可选 AnchorProvider。</summary>
    static CameraAnchorProvider ResolveProvider(Transform root)
    {
        CameraAnchorProvider provider = root.GetComponentInParent<CameraAnchorProvider>();
        return provider != null ? provider : root.GetComponentInChildren<CameraAnchorProvider>(true);
    }

    /// <summary>Binding 或 Shot 变化时仅重采一次进入帧 Pose，拖 Handle 不触发动画重采。</summary>
    void EnsureSnapshotCache(CameraShotNotifyState shot, CameraAnchorProvider characterProvider)
    {
        int bindingHash = ComputeBindingHash(shot.ReferenceBinding, shot.LookAtBinding);
        if (ReferenceEquals(_snapshotShot, shot) && _snapshotBindingHash == bindingHash)
            return;

        InvalidateSnapshotCache();
        _snapshotShot = shot;
        _snapshotBindingHash = bindingHash;
        if (shot.ReferenceBinding?.Space == CameraBindingSpace.Snapshot)
        {
            _hasReferenceSnapshot = TryEvaluateSnapshotBinding(
                shot.ReferenceBinding,
                shot.StartFrame,
                characterProvider,
                out _referenceSnapshot);
        }

        if (shot.LookAtBinding?.Space == CameraBindingSpace.Snapshot)
        {
            _hasLookAtSnapshot = TryEvaluateSnapshotBinding(
                shot.LookAtBinding,
                shot.StartFrame,
                characterProvider,
                out _lookAtSnapshot);
        }
    }

    /// <summary>Snapshot 读缓存，Dynamic/World 使用当前预览帧的共享 Binding 解析。</summary>
    bool TryResolvePreviewBinding(
        CameraTransformBinding binding,
        bool reference,
        Transform previewTarget,
        CameraAnchorProvider characterProvider,
        out CameraReferencePose pose)
    {
        if (binding != null && binding.Space == CameraBindingSpace.Snapshot)
        {
            pose = reference ? _referenceSnapshot : _lookAtSnapshot;
            return reference ? _hasReferenceSnapshot : _hasLookAtSnapshot;
        }

        return CameraShotPoseResolver.TryResolveReferencePose(
            binding,
            _context.PreviewCharacter,
            previewTarget,
            characterProvider,
            null,
            out pose);
    }

    /// <summary>按 Shot StartFrame 采样 Character；预览目标以该进入 Pose 前方的固定点近似。</summary>
    bool TryEvaluateSnapshotBinding(
        CameraTransformBinding binding,
        int frame,
        CameraAnchorProvider characterProvider,
        out CameraReferencePose pose)
    {
        pose = CameraReferencePose.Identity;
        if (binding == null)
            return false;
        if (binding.Source == CameraBindingSource.World)
            return true;
        if (_worldPoseEvaluator == null)
            return false;

        string attachPointName = null;
        if (binding.Source == CameraBindingSource.Character
            && !string.IsNullOrWhiteSpace(binding.AnchorId))
        {
            if (characterProvider == null
                || !characterProvider.TryResolveCameraAnchor(binding.AnchorId, out Transform anchor)
                || anchor == null)
            {
                return false;
            }
            attachPointName = anchor.name;
        }

        if (!_worldPoseEvaluator(
                frame,
                attachPointName,
                Vector3.zero,
                Vector3.zero,
                out Vector3 position,
                out Quaternion rotation))
        {
            return false;
        }

        if (binding.Source == CameraBindingSource.SelectedTarget)
            position += rotation * Vector3.forward * 3f;
        pose = new CameraReferencePose(position, rotation);
        return true;
    }

    /// <summary>生成 Snapshot 缓存键；Action 对象内字段变更后立即重采一次。</summary>
    static int ComputeBindingHash(
        CameraTransformBinding referenceBinding,
        CameraTransformBinding lookAtBinding)
    {
        unchecked
        {
            int hash = ComputeBindingHash(referenceBinding);
            return (hash * 397) ^ ComputeBindingHash(lookAtBinding);
        }
    }

    /// <summary>把单个 Binding 的来源、空间和 AnchorId 合入缓存键。</summary>
    static int ComputeBindingHash(CameraTransformBinding binding)
    {
        if (binding == null)
            return 0;
        unchecked
        {
            int hash = (int)binding.Source;
            hash = (hash * 397) ^ (int)binding.Space;
            return (hash * 397) ^ (binding.AnchorId?.GetHashCode() ?? 0);
        }
    }

    /// <summary>清除进入帧 Pose；下一个有效 Shot 再按需采样。</summary>
    void InvalidateSnapshotCache()
    {
        _snapshotShot = null;
        _snapshotBindingHash = 0;
        _hasReferenceSnapshot = false;
        _hasLookAtSnapshot = false;
    }

    /// <summary>按固定上限采样局部 Spline；不再逐点重采整段动画。</summary>
    void DrawSplinePath(Spline spline, CameraReferencePose referencePose)
    {
        if (!CameraSplineEvaluator.IsValid(spline))
            return;

        for (int i = 0; i <= PathSamples; i++)
        {
            float3 local = spline.EvaluatePosition(i / (float)PathSamples);
            _pathPoints[i] = referencePose.TransformPoint(ToVector3(local));
        }

        Handles.color = new Color(0.2f, 0.85f, 1f, 0.85f);
        Handles.DrawAAPolyLine(2f, _pathPoints);
    }

    /// <summary>预设规则只开放首尾位置；Custom 才开放全部 Knot、旋转与切线。</summary>
    void DrawSplineHandles(
        Spline spline,
        CameraReferencePose referencePose,
        CameraSplineCurveRule curveRule)
    {
        if (spline == null || spline.Count == 0)
            return;

        bool custom = curveRule == CameraSplineCurveRule.Custom;
        for (int i = 0; i < spline.Count; i++)
        {
            if (!custom && i > 0 && i < spline.Count - 1)
                continue;
            Vector3 worldPosition = referencePose.TransformPoint(ToVector3(spline[i].Position));
            DrawKnotSelectionHandle(i, worldPosition);
        }

        // 未显式点击 Knot 时只显示可选点，不接管 Unity 的 W/E 与 Scene 飞行浏览。
        if (_selectedKnot < 0 || _selectedKnot >= spline.Count)
            return;

        BezierKnot knot = spline[_selectedKnot];
        Vector3 knotWorld = referencePose.TransformPoint(ToVector3(knot.Position));
        if (custom && Tools.current == Tool.Rotate)
        {
            DrawKnotRotationHandle(spline, referencePose, knot, knotWorld);
            return;
        }
        if (Tools.current != Tool.Move)
            return;

        EditorGUI.BeginChangeCheck();
        Vector3 movedKnot = Handles.PositionHandle(knotWorld, referencePose.Rotation);
        if (EditorGUI.EndChangeCheck())
        {
            RecordChange("Move Camera Spline Knot");
            knot.Position = ToFloat3(InverseTransformPoint(referencePose, movedKnot));
            spline[_selectedKnot] = knot;
            CameraSplineCurveRuleUtility.Apply(spline, curveRule);
            MarkChanged();
        }

        if (!custom)
            return;

        TangentMode mode = spline.GetTangentMode(_selectedKnot);
        if (mode == TangentMode.AutoSmooth || mode == TangentMode.Linear)
            return;

        // 开放路径首尾只显示参与实际曲段的一侧，避免无效切线误导作者。
        if (spline.Closed || _selectedKnot > 0)
            DrawTangentHandle(spline, referencePose, _selectedKnot, BezierTangent.In);
        if (spline.Closed || _selectedKnot < spline.Count - 1)
            DrawTangentHandle(spline, referencePose, _selectedKnot, BezierTangent.Out);
    }

    /// <summary>SceneView 获得焦点时响应 W；只有 Custom 路径响应 E 旋转工具。</summary>
    static void HandleToolShortcuts(bool allowRotate)
    {
        Event current = Event.current;
        if (current.type != EventType.KeyDown
            || current.control
            || current.command
            || current.alt
            || current.shift
            || GUIUtility.hotControl != 0
            || EditorGUIUtility.editingTextField)
        {
            return;
        }

        if (current.keyCode == KeyCode.W)
            Tools.current = Tool.Move;
        else if (allowRotate && current.keyCode == KeyCode.E)
            Tools.current = Tool.Rotate;
        else
            return;

        current.Use();
        SceneView.RepaintAll();
    }

    /// <summary>用独立 ControlId 扩大 Knot 点击热区，避免 Scene 对象或 PositionHandle 抢占点击。</summary>
    void DrawKnotSelectionHandle(int knotIndex, Vector3 worldPosition)
    {
        int controlId = GUIUtility.GetControlID(KnotControlHint + knotIndex, FocusType.Passive);
        float visualSize = HandleUtility.GetHandleSize(worldPosition) * 0.13f;
        float pickSize = visualSize * 2.8f;
        Event current = Event.current;
        switch (current.GetTypeForControl(controlId))
        {
            case EventType.Layout:
                HandleUtility.AddControl(
                    controlId,
                    HandleUtility.DistanceToCircle(worldPosition, pickSize));
                break;
            case EventType.Repaint:
                Handles.color = knotIndex == _selectedKnot ? Color.yellow : Color.cyan;
                Handles.SphereHandleCap(
                    controlId,
                    worldPosition,
                    Quaternion.identity,
                    visualSize,
                    EventType.Repaint);
                break;
            case EventType.MouseDown:
                if (current.button != 0
                    || current.alt
                    || HandleUtility.nearestControl != controlId)
                {
                    break;
                }

                GUIUtility.hotControl = controlId;
                _selectedKnot = knotIndex;
                current.Use();
                SceneView.RepaintAll();
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl != controlId)
                    break;
                GUIUtility.hotControl = 0;
                current.Use();
                break;
        }
    }

    /// <summary>按当前预览帧求值出的相机 Position、LookAt 与 FOV 绘制不会抢占点击的视锥。</summary>
    static void DrawCurrentFrameFrustum(
        SceneView sceneView,
        CameraShotPose currentPose)
    {
        Vector3 origin = currentPose.WorldPosition;
        Vector3 lookDirection = currentPose.WorldLookAt - origin;
        if (lookDirection.sqrMagnitude <= 0.000001f)
            return;

        Quaternion rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
        Camera gameCamera = Camera.main;
        float aspect = gameCamera != null && gameCamera.aspect > 0.01f
            ? gameCamera.aspect
            : sceneView?.camera != null && sceneView.camera.aspect > 0.01f
                ? sceneView.camera.aspect
                : 16f / 9f;
        float halfAngleRadians = Mathf.Clamp(currentPose.FieldOfView, 1f, 179f)
            * 0.5f
            * Mathf.Deg2Rad;
        float depth = Mathf.Clamp(lookDirection.magnitude, 0.5f, 3f);
        float halfHeight = Mathf.Tan(halfAngleRadians) * depth;
        float halfWidth = halfHeight * aspect;

        // 极端 FOV 时缩短视锥深度，但保持投影视角不变，避免覆盖整个 Scene。
        float largestExtent = Mathf.Max(halfHeight, halfWidth);
        if (largestExtent > 5f)
        {
            float scale = 5f / largestExtent;
            depth *= scale;
            halfHeight *= scale;
            halfWidth *= scale;
        }

        Vector3 forward = rotation * Vector3.forward * depth;
        Vector3 right = rotation * Vector3.right * halfWidth;
        Vector3 up = rotation * Vector3.up * halfHeight;
        Vector3 topLeft = origin + forward - right + up;
        Vector3 topRight = origin + forward + right + up;
        Vector3 bottomRight = origin + forward + right - up;
        Vector3 bottomLeft = origin + forward - right - up;

        using (new Handles.DrawingScope(new Color(1f, 0.82f, 0.18f, 0.9f)))
        {
            Handles.DrawLine(origin, topLeft);
            Handles.DrawLine(origin, topRight);
            Handles.DrawLine(origin, bottomRight);
            Handles.DrawLine(origin, bottomLeft);
            Handles.DrawLine(topLeft, topRight);
            Handles.DrawLine(topRight, bottomRight);
            Handles.DrawLine(bottomRight, bottomLeft);
            Handles.DrawLine(bottomLeft, topLeft);
            Handles.Label(topLeft, $"FOV {currentPose.FieldOfView:0.#}°");
        }
    }

    /// <summary>用 E/Rotate 工具编辑 BezierKnot.Rotation，使 Knot 与切线局部朝向同步旋转。</summary>
    void DrawKnotRotationHandle(
        Spline spline,
        CameraReferencePose referencePose,
        BezierKnot knot,
        Vector3 knotWorld)
    {
        Quaternion localRotation = ToQuaternion(knot.Rotation);
        Quaternion worldRotation = referencePose.Rotation * localRotation;
        EditorGUI.BeginChangeCheck();
        Quaternion movedRotation = Handles.RotationHandle(worldRotation, knotWorld);
        if (!EditorGUI.EndChangeCheck())
            return;

        RecordChange("Rotate Camera Spline Knot");
        Quaternion movedLocal = Quaternion.Inverse(referencePose.Rotation) * movedRotation;
        knot.Rotation = new quaternion(
            movedLocal.x,
            movedLocal.y,
            movedLocal.z,
            movedLocal.w);
        spline[_selectedKnot] = knot;
        MarkChanged();
    }

    /// <summary>将官方 BezierKnot 的旋转局部 Tangent 映射为 Scene 世界手柄。</summary>
    void DrawTangentHandle(
        Spline spline,
        CameraReferencePose referencePose,
        int knotIndex,
        BezierTangent tangentSide)
    {
        BezierKnot knot = spline[knotIndex];
        float3 tangent = tangentSide == BezierTangent.In ? knot.TangentIn : knot.TangentOut;
        float3 rotated = math.rotate(knot.Rotation, tangent);
        Vector3 knotWorld = referencePose.TransformPoint(ToVector3(knot.Position));
        Vector3 tangentWorld = referencePose.TransformPoint(ToVector3(knot.Position + rotated));
        Handles.color = tangentSide == BezierTangent.In ? new Color(1f, 0.55f, 0.2f) : Color.magenta;
        Handles.DrawLine(knotWorld, tangentWorld);

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.PositionHandle(tangentWorld, referencePose.Rotation);
        if (!EditorGUI.EndChangeCheck())
            return;

        RecordChange("Move Camera Spline Tangent");
        float3 localVector = ToFloat3(InverseTransformPoint(referencePose, moved)) - knot.Position;
        float3 unrotated = math.rotate(math.inverse(knot.Rotation), localVector);
        if (tangentSide == BezierTangent.In)
            knot.TangentIn = unrotated;
        else
            knot.TangentOut = unrotated;
        spline.SetKnot(knotIndex, knot, tangentSide);
        MarkChanged();
    }

    /// <summary>绘制 Scene 左上样条编辑工具与 W/E 快捷键提示。</summary>
    void DrawSceneOverlay(
        SceneView sceneView,
        CameraShotNotifyState shot,
        CameraReferencePose referencePose,
        CameraReferencePose lookAtPose,
        CameraShotPose currentPose)
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10f, 10f, 310f, 350f), EditorStyles.helpBox);
        GUILayout.Label($"Camera Spline: {shot.Id}");
        EditorGUILayout.HelpBox(
            "黄色视锥显示当前预览帧。预设规则只需 W 拖首尾端点；Custom 才开放 E 旋转和 Tangent。"
            + " 未选点或使用 Ctrl/Shift/Alt/右键浏览时不接管快捷键。",
            MessageType.Info);
        if (GUILayout.Button("打开 Camera View"))
            ActionEditorCameraView.Open();

        Spline spline = shot.PositionSpline;
        bool custom = shot.SplineCurveRule == CameraSplineCurveRule.Custom;
        GUILayout.Label($"Curve Rule: {shot.SplineCurveRule}");
        if (spline != null && spline.Count >= 2)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("选择端点", GUILayout.Width(62f));
            if (GUILayout.Toggle(_selectedKnot == 0, "起点", "Button"))
                _selectedKnot = 0;
            int endIndex = spline.Count - 1;
            if (GUILayout.Toggle(_selectedKnot == endIndex, "终点", "Button"))
                _selectedKnot = endIndex;
            GUILayout.EndHorizontal();
        }

        _sceneCaptureFovMode = (SceneCaptureFovMode)EditorGUILayout.EnumPopup(
            new GUIContent(
                "取景 FOV",
                "Keep Shot 不修改；Scene View 读取当前 Scene 相机；Custom 使用下方数值。"),
            _sceneCaptureFovMode);
        if (_sceneCaptureFovMode == SceneCaptureFovMode.Custom)
        {
            _customCaptureFov = EditorGUILayout.Slider(
                new GUIContent("Custom FOV", "写入当前预览帧的 FOV Curve Key。"),
                _customCaptureFov,
                1f,
                179f);
        }

        bool sceneFovUnavailable = _sceneCaptureFovMode == SceneCaptureFovMode.SceneView
            && sceneView != null
            && sceneView.orthographic;
        bool canApplySceneView = sceneView?.camera != null
            && spline != null
            && _selectedKnot >= 0
            && _selectedKnot < spline.Count
            && !sceneFovUnavailable;
        using (new EditorGUI.DisabledScope(!canApplySceneView))
        {
            if (GUILayout.Button(new GUIContent(
                    "Scene 构图 → 选中点",
                    "写入位置并按 Scene 朝向更新 LookAt 偏移；FOV Key 写入当前预览帧。")))
            {
                ApplySceneViewCompositionToSelectedKnot(
                    sceneView,
                    shot,
                    referencePose,
                    lookAtPose,
                    currentPose);
            }
        }
        if (sceneFovUnavailable)
            EditorGUILayout.HelpBox("正交 Scene 视图没有可用的透视 FOV；请切回透视或选择 Keep Shot/Custom。", MessageType.Warning);

        GUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!custom))
        {
            if (GUILayout.Button("插入 Knot"))
                InsertKnot(shot.PositionSpline);
            using (new EditorGUI.DisabledScope(
                       shot.PositionSpline == null
                       || shot.PositionSpline.Count <= 2
                       || _selectedKnot < 0))
            {
                if (GUILayout.Button("删除 Knot"))
                    RemoveSelectedKnot(shot.PositionSpline);
            }
        }
        if (GUILayout.Button("反转"))
            ReverseSpline(shot.PositionSpline);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!custom))
        {
            if (GUILayout.Button("自动平滑"))
                SetAllTangentModes(shot.PositionSpline, TangentMode.AutoSmooth);
            if (GUILayout.Button("切线水平"))
                FlattenTangents(shot.PositionSpline);
        }
        if (GUILayout.Button("框选路径"))
            FramePath(sceneView, shot.PositionSpline, referencePose);
        GUILayout.EndHorizontal();

        if (custom && spline != null && _selectedKnot >= 0 && _selectedKnot < spline.Count)
        {
            int knotIndex = _selectedKnot;
            TangentMode mode = spline.GetTangentMode(knotIndex);
            TangentMode nextMode = (TangentMode)EditorGUILayout.EnumPopup("选中 Knot 模式", mode);
            if (nextMode != mode)
                SetSelectedTangentMode(spline, knotIndex, nextMode);
        }

        GUILayout.EndArea();
        Handles.EndGUI();
    }

    /// <summary>把 SceneView 的位置与朝向写回 Knot/LookAt，并按选项写入当前帧 FOV Key。</summary>
    void ApplySceneViewCompositionToSelectedKnot(
        SceneView sceneView,
        CameraShotNotifyState shot,
        CameraReferencePose referencePose,
        CameraReferencePose lookAtPose,
        CameraShotPose currentPose)
    {
        Spline spline = shot?.PositionSpline;
        if (sceneView?.camera == null
            || spline == null
            || _selectedKnot < 0
            || _selectedKnot >= spline.Count)
        {
            return;
        }

        RecordChange("Apply Scene View To Camera Spline Knot");
        Transform sceneCamera = sceneView.camera.transform;
        BezierKnot knot = spline[_selectedKnot];
        knot.Position = ToFloat3(
            InverseTransformPoint(referencePose, sceneCamera.position));
        spline[_selectedKnot] = knot;

        float lookDistance = Vector3.Distance(
            currentPose.WorldPosition,
            currentPose.WorldLookAt);
        if (lookDistance < 0.01f)
            lookDistance = Mathf.Max(1f, sceneView.size);
        Vector3 worldLookAt = sceneCamera.position + sceneCamera.forward * lookDistance;
        shot.SetLookAtLocalPosition(InverseTransformPoint(lookAtPose, worldLookAt));

        float capturedFov = _sceneCaptureFovMode switch
        {
            SceneCaptureFovMode.SceneView => sceneView.camera.fieldOfView,
            SceneCaptureFovMode.Custom => _customCaptureFov,
            _ => -1f,
        };
        if (capturedFov > 0f)
            shot.SetFieldOfViewKey(shot.EvaluateNormalizedTime(_context.PreviewFrame), capturedFov);

        // 预设路径只允许端点驱动，写入位置后立即重算其自动切线。
        CameraSplineCurveRuleUtility.Apply(spline, shot.SplineCurveRule);
        MarkChanged();
    }

    /// <summary>在选中 Knot 后插入中点；末端则沿末段方向延伸。</summary>
    void InsertKnot(Spline spline)
    {
        if (spline == null || spline.Count == 0)
            return;

        RecordChange("Insert Camera Spline Knot");
        int current = _selectedKnot >= 0
            ? Mathf.Clamp(_selectedKnot, 0, spline.Count - 1)
            : spline.Count - 1;
        int insertIndex = current + 1;
        float3 position;
        if (insertIndex < spline.Count)
            position = math.lerp(spline[current].Position, spline[insertIndex].Position, 0.5f);
        else
        {
            float3 direction = current > 0
                ? spline[current].Position - spline[current - 1].Position
                : new float3(0f, 0f, 1f);
            position = spline[current].Position + direction;
        }

        spline.Insert(insertIndex, new BezierKnot(position), TangentMode.AutoSmooth);
        _selectedKnot = insertIndex;
        MarkChanged();
    }

    /// <summary>保持有效路径至少两个 Knot，删除当前选中点。</summary>
    void RemoveSelectedKnot(Spline spline)
    {
        if (spline == null
            || spline.Count <= 2
            || _selectedKnot < 0
            || _selectedKnot >= spline.Count)
            return;
        RecordChange("Remove Camera Spline Knot");
        spline.RemoveAt(_selectedKnot);
        _selectedKnot = Mathf.Clamp(_selectedKnot, 0, spline.Count - 1);
        MarkChanged();
    }

    /// <summary>调用官方 Utility 反转流向并保持曲线形状。</summary>
    void ReverseSpline(Spline spline)
    {
        if (!CameraSplineEvaluator.IsValid(spline))
            return;
        RecordChange("Reverse Camera Spline");
        SplineUtility.ReverseFlow(spline);
        if (_selectedKnot >= 0)
            _selectedKnot = spline.Count - 1 - Mathf.Clamp(_selectedKnot, 0, spline.Count - 1);
        MarkChanged();
    }

    /// <summary>批量设置官方 TangentMode。</summary>
    void SetAllTangentModes(Spline spline, TangentMode mode)
    {
        if (spline == null || spline.Count == 0)
            return;
        RecordChange("Smooth Camera Spline");
        spline.SetTangentMode(mode);
        MarkChanged();
    }

    /// <summary>切换单个 Knot 的官方 TangentMode，并由 Spline 修正切线约束。</summary>
    void SetSelectedTangentMode(Spline spline, int knotIndex, TangentMode mode)
    {
        RecordChange("Change Camera Spline Tangent Mode");
        spline.SetTangentMode(knotIndex, mode);
        MarkChanged();
    }

    /// <summary>将切线 Y 分量清零并切为 Broken，便于制作水平绕行段。</summary>
    void FlattenTangents(Spline spline)
    {
        if (spline == null || spline.Count == 0)
            return;
        RecordChange("Flatten Camera Spline Tangents");
        for (int i = 0; i < spline.Count; i++)
        {
            spline.SetTangentMode(i, TangentMode.Broken);
            BezierKnot knot = spline[i];
            knot.TangentIn.y = 0f;
            knot.TangentOut.y = 0f;
            spline[i] = knot;
        }
        MarkChanged();
    }

    /// <summary>按样条采样 Bounds 将 SceneView 框选到整条路径。</summary>
    static void FramePath(SceneView sceneView, Spline spline, CameraReferencePose referencePose)
    {
        if (!CameraSplineEvaluator.IsValid(spline))
            return;
        Vector3 first = referencePose.TransformPoint(ToVector3(spline.EvaluatePosition(0f)));
        var bounds = new Bounds(first, Vector3.one * 0.1f);
        for (int i = 1; i <= PathSamples; i++)
        {
            Vector3 point = referencePose.TransformPoint(
                ToVector3(spline.EvaluatePosition(i / (float)PathSamples)));
            bounds.Encapsulate(point);
        }
        sceneView.Frame(bounds, false);
    }

    /// <summary>记录嵌套 Spline 修改，保证 Ctrl+Z 能恢复整个 ActionDefinition。</summary>
    void RecordChange(string undoName)
    {
        SerializedObject so = _serializedObjectGetter?.Invoke();
        if (so?.targetObject != null)
            Undo.RecordObject(so.targetObject, undoName);
    }

    /// <summary>标记 ActionDefinition 脏并刷新 SerializedObject/Scene。</summary>
    void MarkChanged()
    {
        SerializedObject so = _serializedObjectGetter?.Invoke();
        if (so?.targetObject == null)
            return;
        EditorUtility.SetDirty(so.targetObject);
        so.Update();
        SceneView.RepaintAll();
    }

    /// <summary>仅当前选中块属于 Camera Track 时开放编辑手柄。</summary>
    bool IsCameraWindowSelected()
    {
        ActionEditorSelection selection = _selectionGetter?.Invoke() ?? default;
        return selection.IsValid && selection.Kind == ActionTimelineTrackKind.Camera;
    }

    /// <summary>Binding 缺失时显示明确错误，禁止静默回退角色根。</summary>
    static void DrawBindingError()
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10f, 10f, 340f, 50f), EditorStyles.helpBox);
        EditorGUILayout.HelpBox("Camera Binding 无法解析：请检查 Root、SelectedTarget 或 CameraAnchorProvider。", MessageType.Error);
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    static Vector3 ToVector3(float3 value) => new(value.x, value.y, value.z);

    static float3 ToFloat3(Vector3 value) => new(value.x, value.y, value.z);

    static Quaternion ToQuaternion(quaternion value) =>
        new(value.value.x, value.value.y, value.value.z, value.value.w);

    static Vector3 InverseTransformPoint(CameraReferencePose pose, Vector3 worldPoint) =>
        Quaternion.Inverse(pose.Rotation) * (worldPoint - pose.Position);
}

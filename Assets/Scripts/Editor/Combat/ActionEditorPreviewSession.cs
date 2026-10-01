using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ActionDefinition 编辑器 Scene 预览会话：驱动动画采样并按序调用各 PreviewExtension。
/// 全局同时仅允许一个活跃 Session（AnimationMode 为全局状态）。
/// BaseMotionMode=BakedMotion 时按烘焙表挪动预览根，并写 VisualMotionRoot 残差。
/// </summary>
public sealed class ActionEditorPreviewSession : IDisposable
{
    const string VisualMotionRootName = "CharacterVisualMotionRoot";

    static ActionEditorPreviewSession s_globalActive;

    readonly List<IActionEditorPreviewExtension> _extensions = new();
    readonly UnityEngine.Object _owner;

    ActionDefinition _action;
    Transform _previewCharacter;
    int _previewFrame;
    int _lastSampledFrame = int.MinValue;
    AnimationClip _lastSampledClip;
    Transform _lastSampledCharacter;
    bool _extensionsBegun;
    int _inputMovementDirection;

    /// <summary>仅覆盖预览片段，不改变动作资产、逻辑帧和输入。</summary>
    public int InputMovementDirection
    {
        get => _inputMovementDirection;
        set { if (_inputMovementDirection == value) return; _inputMovementDirection = Mathf.Clamp(value, 0, 4); InvalidateSampleCache(); }
    }

    // 烘焙位移预览：相对会话原点累计，结束时还原，避免弄脏场景角色
    bool _hasBakedPreviewOrigin;
    Vector3 _bakedPreviewOriginPosition;
    Quaternion _bakedPreviewOriginRotation;
    Transform _visualMotionRoot;
    Vector3 _visualMotionRootRestLocal;

    /// <summary>owner 可为 CustomEditor 或 EditorWindow；销毁后 Session 停止 Tick。</summary>
    public ActionEditorPreviewSession(UnityEngine.Object owner)
    {
        _owner = owner;
    }

    public void RegisterExtension(IActionEditorPreviewExtension extension)
    {
        if (extension != null && !_extensions.Contains(extension))
            _extensions.Add(extension);
    }

    public void SetAction(ActionDefinition action)
    {
        if (_action == action)
            return;

        EndExtensionsIfNeeded();
        RestoreBakedMotionPreview();
        ActionEditorAnimationSampler.EndSession();
        _action = action;
        InvalidateSampleCache();
    }

    public void SetPreviewCharacter(Transform previewCharacter)
    {
        if (_previewCharacter == previewCharacter)
            return;

        EndExtensionsIfNeeded();
        RestoreBakedMotionPreview();
        ActionEditorAnimationSampler.EndSession();
        _previewCharacter = previewCharacter;
        InvalidateSampleCache();
    }

    /// <summary>
    /// 烘焙预览原点（角色被挪动前的世界位姿）。
    /// 供 Scene 轨迹线相对原点绘制，避免跟在已位移的根上画偏。
    /// </summary>
    public bool TryGetBakedPreviewOrigin(out Vector3 position, out Quaternion rotation)
    {
        if (_hasBakedPreviewOrigin)
        {
            position = _bakedPreviewOriginPosition;
            rotation = _bakedPreviewOriginRotation;
            return true;
        }

        if (_previewCharacter != null)
        {
            position = _previewCharacter.position;
            rotation = _previewCharacter.rotation;
            return true;
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;
        return false;
    }

    /// <summary>
    /// 临时采样到指定逻辑帧（含烘焙位移）以读取挂点世界位姿，再恢复当前预览帧。
    /// 供 parentToAttachPoint=false 的 VFX 在触发帧冻结世界落点。
    /// </summary>
    public bool TryEvaluateAttachWorldPoseAtFrame(
        int frame,
        string attachPointId,
        Vector3 localOffset,
        Vector3 localEuler,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        worldPosition = Vector3.zero;
        worldRotation = Quaternion.identity;
        if (_action == null
            || _previewCharacter == null
            || !ActionEditorAnimationSampler.IsSessionActive)
        {
            return false;
        }

        int restoreFrame = _previewFrame;
        SamplePoseAndBakedMotionAtFrame(frame);

        Transform anchor = ActionEditorPreviewAttachPoint.Resolve(_previewCharacter, attachPointId);
        bool ok = anchor != null;
        if (ok)
        {
            worldPosition = anchor.TransformPoint(localOffset);
            worldRotation = anchor.rotation * Quaternion.Euler(localEuler);
        }

        SamplePoseAndBakedMotionAtFrame(restoreFrame);
        return ok;
    }

    /// <summary>
    /// 临时采样到指定逻辑帧构建 Hitbox 世界 OBB，再恢复当前预览帧。
    /// 供 parentToAttachPoint=false 的 Hitbox 在窗口进入帧冻结。
    /// </summary>
    public bool TryEvaluateHitboxWorldBoxAtFrame(
        int frame,
        HitboxNotifyState hitbox,
        out HitboxOrientedBox box)
    {
        box = default;
        if (hitbox == null
            || _action == null
            || _previewCharacter == null
            || !ActionEditorAnimationSampler.IsSessionActive)
        {
            return false;
        }

        int restoreFrame = _previewFrame;
        SamplePoseAndBakedMotionAtFrame(frame);

        Transform anchor = ActionEditorPreviewAttachPoint.Resolve(
            _previewCharacter,
            hitbox.AttachPointId);
        bool ok = anchor != null;
        if (ok)
            box = HitboxMath.BuildFromHitbox(_previewCharacter, anchor, hitbox);

        SamplePoseAndBakedMotionAtFrame(restoreFrame);
        return ok;
    }

    /// <summary>采样动画 Pose 并贴烘焙位移到指定逻辑帧（不改 Session 的 PreviewFrame 缓存语义）。</summary>
    void SamplePoseAndBakedMotionAtFrame(int frame)
    {
        ActionFrameQueryResult query = ActionFrameQuery.Query(_action, frame);
        AnimationClip clip = query.HasAnimationSegment ? query.Segment.clip : null;
        float sampleRate = _action.SampleRate;
        float localTime = query.SegmentLocalTime;
        ResolveInputMovementPreview(frame, ref clip, ref localTime);
        ActionEditorAnimationSampler.Sample(clip, localTime, sampleRate);

        var context = new ActionEditorPreviewContext(
            _action,
            _previewCharacter,
            ActionEditorPreviewAttachPoint.Resolve(_previewCharacter),
            frame);
        ApplyBakedMotionPreview(context);
    }

    public void SetPreviewFrame(int previewFrame)
    {
        if (_previewFrame == previewFrame)
            return;

        _previewFrame = previewFrame;
        InvalidateSampleCache();
    }

    /// <summary>每 Editor 帧调用：采样动画 Pose，再驱动扩展预览（VFX 等）。</summary>
    public void Tick()
    {
        // 选片预览占用 AnimationMode 时，原窗口保留帧与目标，退出后重新采样。
        if (ActionAnimationPickerPanel.PreviewOwner != null && ActionAnimationPickerPanel.PreviewOwner != _owner)
        {
            if (s_globalActive == this) { EndPreviewState(); s_globalActive = null; }
            return;
        }
        // Play 模式不跑 Editor 预览，避免和运行时动画抢 Graph
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EndPreviewState();
            return;
        }

        if (_owner == null || _action == null || _previewCharacter == null)
        {
            EndPreviewState();
            return;
        }

        EnsureGlobalActive();

        ActionEditorPreviewContext context = BuildContext();
        if (!context.IsValid)
        {
            EndPreviewState();
            return;
        }

        if (!ActionEditorAnimationSampler.BeginSession(_previewCharacter))
        {
            EndPreviewState();
            return;
        }

        // 在 AnimationMode 采样前锁定世界原点，避免 Clip 根曲线污染捕获位姿
        if (ShouldPreviewBakedMotion(context.Action))
            EnsureBakedPreviewOrigin();

        bool resampled = false;
        if (NeedsResample())
        {
            // 与运行时同一套 ActionFrameQuery，禁止第二套窗口算法
            ActionFrameQueryResult query =
                ActionFrameQuery.Query(context.Action, context.PreviewFrame);
            AnimationClip clip = query.HasAnimationSegment ? query.Segment.clip : null;
            float localTime = query.SegmentLocalTime;
            ResolveInputMovementPreview(context.PreviewFrame, ref clip, ref localTime);
            ActionEditorAnimationSampler.Sample(clip, localTime, context.SampleRate);

            _lastSampledFrame = _previewFrame;
            _lastSampledClip = clip;
            _lastSampledCharacter = _previewCharacter;
            resampled = true;
        }

        // 采样后按烘焙表贴位移；须在 Extension 之前，使挂点/VFX 读到已偏移根
        ApplyBakedMotionPreview(context);

        BeginExtensionsIfNeeded(context);

        // 重建 context：根已偏移，Extension 读最新挂点
        context = BuildContext();
        for (int i = 0; i < _extensions.Count; i++)
            _extensions[i].OnPreviewUpdate(in context);

        // 仅在 Pose 实际重采样时刷新 Scene，避免 EditorApplication.update 每帧 RepaintAll。
        if (resampled)
            SceneView.RepaintAll();
    }

    public void Dispose()
    {
        EndPreviewState();

        if (s_globalActive == this)
            s_globalActive = null;
    }

    /// <summary>停止 AnimationMode 与各 Extension，但保留 Session 对象供下次 Tick 复用。</summary>
    void EndPreviewState()
    {
        ActionEditorPreviewContext context = BuildContext();
        EndExtensionsIfNeeded(in context);
        RestoreBakedMotionPreview();
        ActionEditorAnimationSampler.EndSession();
        InvalidateSampleCache();
        _extensionsBegun = false;
    }

    /// <summary>
    /// BaseMotionMode=BakedMotion 时：预览根跟 Gameplay 累计位移，VisualMotionRoot 跟视觉残差。
    /// 非 Baked 或表未就绪时还原到原点。
    /// </summary>
    void ApplyBakedMotionPreview(in ActionEditorPreviewContext context)
    {
        if (_previewCharacter == null)
            return;

        ActionDefinition action = context.Action;
        if (!ShouldPreviewBakedMotion(action))
        {
            RestoreBakedMotionPreview();
            return;
        }

        ActionBakedMotion baked = action.BakedMotion;
        if (!ActionMotionTrajectorySceneDrawing.TryGetCumulativeLocalMeters(
                baked,
                context.PreviewFrame,
                applyPlanarMode: true,
                out Vector3 gameplayLocal))
        {
            RestoreBakedMotionPreview();
            return;
        }

        if (!_hasBakedPreviewOrigin)
            EnsureBakedPreviewOrigin();

        _previewCharacter.SetPositionAndRotation(
            _bakedPreviewOriginPosition + _bakedPreviewOriginRotation * gameplayLocal,
            _bakedPreviewOriginRotation);

        // 视觉残差：与运行时 CharacterVisualMotionBridge 同源查表
        if (_visualMotionRoot != null
            && baked.TryGetVisualResidualMm(context.PreviewFrame, out int rx, out int rz))
        {
            _visualMotionRoot.localPosition = new Vector3(
                MotionQuantization.MmToMeters(rx),
                0f,
                MotionQuantization.MmToMeters(rz));
            _visualMotionRoot.localRotation = Quaternion.identity;
        }
    }

    static bool ShouldPreviewBakedMotion(ActionDefinition action) =>
        action != null
        && action.ExecutionPolicy.BaseMotionMode == ActionBaseMotionMode.BakedMotion
        && action.BakedMotion != null
        && action.BakedMotion.IsReady;

    void EnsureBakedPreviewOrigin()
    {
        if (_hasBakedPreviewOrigin || _previewCharacter == null)
            return;

        _bakedPreviewOriginPosition = _previewCharacter.position;
        _bakedPreviewOriginRotation = _previewCharacter.rotation;
        _hasBakedPreviewOrigin = true;

        _visualMotionRoot = FindVisualMotionRoot(_previewCharacter);
        if (_visualMotionRoot != null)
            _visualMotionRootRestLocal = _visualMotionRoot.localPosition;
    }

    /// <summary>把预览根与 VisualMotionRoot 还原到捕获原点，避免离开编辑器后角色停在偏移处。</summary>
    void RestoreBakedMotionPreview()
    {
        if (!_hasBakedPreviewOrigin)
            return;

        if (_previewCharacter != null)
        {
            _previewCharacter.SetPositionAndRotation(
                _bakedPreviewOriginPosition,
                _bakedPreviewOriginRotation);
        }

        if (_visualMotionRoot != null)
        {
            _visualMotionRoot.localPosition = _visualMotionRootRestLocal;
            _visualMotionRoot.localRotation = Quaternion.identity;
        }

        _hasBakedPreviewOrigin = false;
        _visualMotionRoot = null;
    }

    static Transform FindVisualMotionRoot(Transform previewCharacter)
    {
        if (previewCharacter == null)
            return null;

        // Factory：CharacterPresentationRoot / CharacterVisualMotionRoot
        Transform presentation = previewCharacter.Find("CharacterPresentationRoot");
        if (presentation != null)
        {
            Transform underPresentation = presentation.Find(VisualMotionRootName);
            if (underPresentation != null)
                return underPresentation;
        }

        Transform direct = previewCharacter.Find(VisualMotionRootName);
        if (direct != null)
            return direct;

        // 层级名不一致时按名称深搜一次
        Transform[] children = previewCharacter.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == VisualMotionRootName)
                return children[i];
        }

        return null;
    }

    void EnsureGlobalActive()
    {
        if (s_globalActive != null && s_globalActive != this)
            s_globalActive.Dispose();

        s_globalActive = this;
    }

    ActionEditorPreviewContext BuildContext()
    {
        Transform attachPoint = ActionEditorPreviewAttachPoint.Resolve(_previewCharacter);
        return new ActionEditorPreviewContext(_action, _previewCharacter, attachPoint, _previewFrame);
    }

    bool NeedsResample()
    {
        ActionFrameQueryResult query = ActionFrameQuery.Query(_action, _previewFrame);
        AnimationClip clipAtFrame = query.HasAnimationSegment ? query.Segment.clip : null;
        float localTime = query.SegmentLocalTime;
        ResolveInputMovementPreview(_previewFrame, ref clipAtFrame, ref localTime);
        return _previewFrame != _lastSampledFrame
            || clipAtFrame != _lastSampledClip
            || _previewCharacter != _lastSampledCharacter;
    }

    void InvalidateSampleCache()
    {
        _lastSampledFrame = int.MinValue;
        _lastSampledClip = null;
        _lastSampledCharacter = null;
    }

    void ResolveInputMovementPreview(int frame, ref AnimationClip clip, ref float localTime)
    {
        if (_action == null || !_action.IsInputMovementAnimationActive(frame)) return;
        ActionInputMovement movement = _action.GetInputMovementAtFrame(frame);
        if (movement == null || _inputMovementDirection == 0) return;
        clip = movement.ResolveClip(_inputMovementDirection);
        localTime = movement.SampleTime(_action, ActionInputMoveState.Pack(_inputMovementDirection, movement.StartFrame), frame);
    }

    void BeginExtensionsIfNeeded(in ActionEditorPreviewContext context)
    {
        if (_extensionsBegun)
            return;

        for (int i = 0; i < _extensions.Count; i++)
            _extensions[i].OnPreviewBegin(in context);

        _extensionsBegun = true;
    }

    void EndExtensionsIfNeeded()
    {
        ActionEditorPreviewContext context = BuildContext();
        EndExtensionsIfNeeded(in context);
    }

    void EndExtensionsIfNeeded(in ActionEditorPreviewContext context)
    {
        if (!_extensionsBegun)
            return;

        for (int i = 0; i < _extensions.Count; i++)
            _extensions[i].OnPreviewEnd(in context);

        _extensionsBegun = false;
    }
}

/// <summary>
/// 在指定逻辑帧评估挂点世界位姿（含烘焙位移）；失败返回 false。
/// </summary>
public delegate bool ActionEditorVfxWorldPoseEvaluator(
    int frame,
    string attachPointId,
    Vector3 localOffset,
    Vector3 localEuler,
    out Vector3 worldPosition,
    out Quaternion worldRotation);

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>ActionDefinition 编辑器 Scene 预览上下文；供动画采样与各 PreviewExtension 只读消费。</summary>
public readonly struct ActionEditorPreviewContext
{
    public ActionEditorPreviewContext(
        ActionDefinition action,
        Transform previewCharacter,
        Transform attachPoint,
        int previewFrame)
    {
        Action = action;
        PreviewCharacter = previewCharacter;
        AttachPoint = attachPoint;
        PreviewFrame = previewFrame;
        // 无 Action 时仍按全局逻辑 Hz，避免误用旧 30Hz 估时
        SampleRate = action != null ? action.SampleRate : ActionSim.LogicHz;
        PreviewTimeSeconds = SampleRate > 0f ? previewFrame / SampleRate : 0f;
    }

    public ActionDefinition Action { get; }
    public Transform PreviewCharacter { get; }
    public Transform AttachPoint { get; }
    public int PreviewFrame { get; }
    public float SampleRate { get; }
    public float PreviewTimeSeconds { get; }

    public bool IsValid =>
        Action != null
        && PreviewCharacter != null
        && Action.HasAnimation;
}

/// <summary>
/// ActionDefinition 编辑器 Scene 预览扩展点；新增刀光/HITSTOP/镜头等预览时实现此接口并注册到 Session。
/// </summary>
public interface IActionEditorPreviewExtension
{
    /// <summary>Preview Character 与 Action 就绪后调用。</summary>
    void OnPreviewBegin(in ActionEditorPreviewContext context);

    /// <summary>每 Editor 帧调用；动画采样完成后执行，AttachPoint 已与当前帧 Pose 对齐。</summary>
    void OnPreviewUpdate(in ActionEditorPreviewContext context);

    /// <summary>Session 结束或 Preview Character 移除时清理临时对象。</summary>
    void OnPreviewEnd(in ActionEditorPreviewContext context);
}

/// <summary>解析 Preview Character 上 Hitbox / VFX 的挂点；支持 per-item attachPointId。</summary>
public static class ActionEditorPreviewAttachPoint
{
    /// <summary>无指定 id 时返回 Preview Character 根节点。</summary>
    public static Transform Resolve(Transform previewCharacter) =>
        Resolve(previewCharacter, null);

    /// <summary>按 attachPointId 在 Preview Character 层级下查找；空或找不到则回退根节点。</summary>
    public static Transform Resolve(Transform previewCharacter, string attachPointId)
    {
        if (previewCharacter == null)
            return null;

        if (string.IsNullOrWhiteSpace(attachPointId))
            return previewCharacter;

        Transform found = CharacterAttachPointResolver.FindByName(previewCharacter, attachPointId);
        return found != null ? found : previewCharacter;
    }
}

/// <summary>Edit Mode 下用 AnimationMode 将 ActionDefinition 的 Clip 采样到 Preview Character。</summary>
public static class ActionEditorAnimationSampler
{
    static GameObject s_sampleRoot;
    static Animator s_animator;
    static Transform s_boundPreviewCharacter;
    static bool s_animatorWasEnabled;

    public static bool IsSessionActive => AnimationMode.InAnimationMode();

    /// <summary>在 Preview Character 上查找 Animator 采样根节点。</summary>
    public static bool TryResolveSampleRoot(Transform previewCharacter, out GameObject sampleRoot, out Animator animator)
    {
        sampleRoot = null;
        animator = null;

        if (previewCharacter == null)
            return false;

        animator = previewCharacter.GetComponentInChildren<Animator>();

        if (animator == null)
            return false;

        sampleRoot = animator.gameObject;
        return true;
    }

    /// <summary>开启 AnimationMode 并暂时禁用 Animator，避免与采样结果冲突。</summary>
    public static bool BeginSession(Transform previewCharacter)
    {
        // 热路径：同一 Preview Character 已在 AnimationMode 时跳过 GetComponentInChildren。
        if (previewCharacter != null
            && s_boundPreviewCharacter == previewCharacter
            && s_sampleRoot != null
            && IsSessionActive)
            return true;

        if (!TryResolveSampleRoot(previewCharacter, out GameObject sampleRoot, out Animator animator))
            return false;

        if (s_sampleRoot == sampleRoot && IsSessionActive)
        {
            s_boundPreviewCharacter = previewCharacter;
            return true;
        }

        EndSession();

        s_sampleRoot = sampleRoot;
        s_animator = animator;
        s_boundPreviewCharacter = previewCharacter;
        s_animatorWasEnabled = animator.enabled;
        animator.enabled = false;
        AnimationMode.StartAnimationMode();
        return true;
    }

    /// <summary>将 Clip 采样到 previewTimeSeconds；sampleRate 与 ActionDefinition 逻辑帧对齐。</summary>
    public static void Sample(AnimationClip clip, float previewTimeSeconds, float sampleRate)
    {
        if (clip == null || s_sampleRoot == null || !IsSessionActive)
            return;

        // Unity 2022.3 SampleAnimationClip 仅接受 (root, clip, time)；逻辑帧对齐由 previewTimeSeconds 保证。
        float time = Mathf.Clamp(previewTimeSeconds, 0f, clip.length);

        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(s_sampleRoot, clip, time);
        AnimationMode.EndSampling();
    }

    /// <summary>结束采样并恢复 Animator 状态。</summary>
    public static void EndSession()
    {
        if (IsSessionActive)
            AnimationMode.StopAnimationMode();

        if (s_animator != null)
            s_animator.enabled = s_animatorWasEnabled;

        s_sampleRoot = null;
        s_animator = null;
        s_boundPreviewCharacter = null;
    }
}

/// <summary>
/// ActionDefinition 编辑器 Scene 预览会话：驱动动画采样并按序调用各 PreviewExtension。
/// 全局同时仅允许一个活跃 Session（AnimationMode 为全局状态）。
/// </summary>
public sealed class ActionEditorPreviewSession : IDisposable
{
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
        _action = action;
        InvalidateSampleCache();
    }

    public void SetPreviewCharacter(Transform previewCharacter)
    {
        if (_previewCharacter == previewCharacter)
            return;

        EndExtensionsIfNeeded();
        ActionEditorAnimationSampler.EndSession();
        _previewCharacter = previewCharacter;
        InvalidateSampleCache();
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

        bool resampled = false;
        if (NeedsResample())
        {
            ActionFrameQueryResult query =
                ActionFrameQuery.Query(context.Action, context.PreviewFrame);
            AnimationClip clip = query.HasAnimationSegment ? query.Segment.clip : null;
            float localTime = query.SegmentLocalTime;
            ActionEditorAnimationSampler.Sample(clip, localTime, context.SampleRate);

            _lastSampledFrame = _previewFrame;
            _lastSampledClip = clip;
            _lastSampledCharacter = _previewCharacter;
            resampled = true;
        }

        BeginExtensionsIfNeeded(context);

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
        ActionEditorAnimationSampler.EndSession();
        InvalidateSampleCache();
        _extensionsBegun = false;
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
/// VFX 帧事件 Scene 预览扩展：按预览帧驱动全部已触发条目的 Prefab/粒子，无需时间轴选中。
/// </summary>
public sealed class ActionEditorVfxPreviewExtension : IActionEditorPreviewExtension
{
    /// <summary>单条 VFX 预览槽：缓存实例与源 Prefab，避免每帧重建。</summary>
    sealed class PreviewSlot
    {
        public GameObject Instance;
        public GameObject SourcePrefab;
    }

    Func<SerializedProperty> _getVfxArrayProp;
    readonly Dictionary<int, PreviewSlot> _slots = new();
    readonly List<int> _staleSlotKeys = new();
    int _lastSimulatedFrame = int.MinValue;
    bool _lastEnabled;

    /// <summary>关闭时不实例化 Prefab、不驱动粒子模拟。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>由 Editor 注入 timeline.playVfxNotifies 数组属性读取器。</summary>
    public void Bind(Func<SerializedProperty> getVfxArrayProp) => _getVfxArrayProp = getVfxArrayProp;

    /// <summary>关闭预览并立即销毁 Scene 中的全部临时实例。</summary>
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        if (!enabled)
        {
            DestroyAllPreviewInstances();
            _lastSimulatedFrame = int.MinValue;
            _lastEnabled = false;
        }
    }

    public void OnPreviewBegin(in ActionEditorPreviewContext context)
    {
        _lastSimulatedFrame = int.MinValue;
        _lastEnabled = IsEnabled;
    }

    public void OnPreviewUpdate(in ActionEditorPreviewContext context)
    {
        if (!IsEnabled)
        {
            if (_lastEnabled)
            {
                DestroyAllPreviewInstances();
                _lastSimulatedFrame = int.MinValue;
            }

            _lastEnabled = false;
            return;
        }

        SerializedProperty arrayProp = _getVfxArrayProp?.Invoke();
        if (arrayProp == null || !arrayProp.isArray || context.PreviewCharacter == null)
        {
            DestroyAllPreviewInstances();
            _lastSimulatedFrame = int.MinValue;
            _lastEnabled = IsEnabled;
            return;
        }

        // 粒子 Simulate 昂贵：仅 Preview Frame / 刚开启时重采样；Transform 每帧仍同步以支持 Handles。
        bool shouldResimulateParticles =
            !_lastEnabled || context.PreviewFrame != _lastSimulatedFrame;

        int sampleRate = Mathf.Max(1, Mathf.RoundToInt(context.SampleRate));
        var alive = new HashSet<int>();

        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            SerializedProperty vfxProp = arrayProp.GetArrayElementAtIndex(i);
            SerializedProperty startProp = vfxProp.FindPropertyRelative("startFrame");
            SerializedProperty prefabProp = vfxProp.FindPropertyRelative("prefab");
            if (startProp == null || prefabProp == null)
                continue;

            // 触发帧之前不生成实例；Scrub 到对应位置后才显示。
            int trigger = startProp.intValue;
            if (context.PreviewFrame < trigger)
                continue;

            GameObject prefab = prefabProp.objectReferenceValue as GameObject;
            if (prefab == null)
                continue;

            SerializedProperty attachIdProp = vfxProp.FindPropertyRelative("attachPointId");
            string attachId = attachIdProp != null ? attachIdProp.stringValue : null;
            Transform anchor = ActionEditorPreviewAttachPoint.Resolve(context.PreviewCharacter, attachId);
            if (anchor == null)
                continue;

            PreviewSlot slot = EnsureSlot(i, prefab, anchor);
            if (slot?.Instance == null)
                continue;

            alive.Add(i);
            ApplyPreviewTransform(slot.Instance, vfxProp, anchor);

            if (!shouldResimulateParticles)
                continue;

            SerializedProperty speedProp = vfxProp.FindPropertyRelative("playbackSpeed");
            float localTime =
                ActionFrameQuery.GetElapsedSecondsSincePoint(trigger, context.PreviewFrame, sampleRate);
            float speed = speedProp != null ? Mathf.Max(0.0001f, speedProp.floatValue) : 1f;
            ActionVfxEditorPreview.SimulateAt(slot.Instance, localTime * speed);
        }

        DestroySlotsNotIn(alive);
        _lastSimulatedFrame = context.PreviewFrame;
        _lastEnabled = true;

        // 刚开启或帧变化时补刷新；稳态不再每帧 RepaintAll。
        if (shouldResimulateParticles && alive.Count > 0)
            SceneView.RepaintAll();
    }

    public void OnPreviewEnd(in ActionEditorPreviewContext context) => DestroyAllPreviewInstances();

    PreviewSlot EnsureSlot(int index, GameObject prefab, Transform anchor)
    {
        if (_slots.TryGetValue(index, out PreviewSlot slot)
            && slot.Instance != null
            && slot.SourcePrefab == prefab)
            return slot;

        DestroySlot(index);

        var instance = PrefabUtility.InstantiatePrefab(prefab, anchor) as GameObject;
        if (instance == null)
            return null;

        instance.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
        instance.name = $"[VFX Preview {index}] {prefab.name}";
        ActionVfxEditorPreview.RestartParticleSystems(instance);

        slot = new PreviewSlot
        {
            Instance = instance,
            SourcePrefab = prefab,
        };
        _slots[index] = slot;
        return slot;
    }

    static void ApplyPreviewTransform(GameObject instance, SerializedProperty vfxProp, Transform anchor)
    {
        SerializedProperty offsetProp = vfxProp.FindPropertyRelative("localOffset");
        SerializedProperty eulerProp = vfxProp.FindPropertyRelative("localEulerAngles");
        SerializedProperty scaleProp = vfxProp.FindPropertyRelative("localScale");
        SerializedProperty parentProp = vfxProp.FindPropertyRelative("parentToAttachPoint");

        if (offsetProp == null || eulerProp == null || scaleProp == null)
            return;

        bool parentToAttach = parentProp == null || parentProp.boolValue;
        Vector3 safeScale = Vector3.Max(scaleProp.vector3Value, Vector3.one * 0.01f);

        if (parentToAttach)
        {
            instance.transform.SetParent(anchor, false);
            instance.transform.localPosition = offsetProp.vector3Value;
            instance.transform.localRotation = Quaternion.Euler(eulerProp.vector3Value);
            instance.transform.localScale = safeScale;
            return;
        }

        instance.transform.SetParent(null, true);
        instance.transform.position = anchor.TransformPoint(offsetProp.vector3Value);
        instance.transform.rotation = anchor.rotation * Quaternion.Euler(eulerProp.vector3Value);
        instance.transform.localScale = safeScale;
    }

    void DestroySlotsNotIn(HashSet<int> alive)
    {
        _staleSlotKeys.Clear();
        foreach (KeyValuePair<int, PreviewSlot> pair in _slots)
        {
            if (!alive.Contains(pair.Key))
                _staleSlotKeys.Add(pair.Key);
        }

        for (int i = 0; i < _staleSlotKeys.Count; i++)
            DestroySlot(_staleSlotKeys[i]);
    }

    void DestroySlot(int index)
    {
        if (!_slots.TryGetValue(index, out PreviewSlot slot))
            return;

        if (slot.Instance != null)
            UnityEngine.Object.DestroyImmediate(slot.Instance);

        _slots.Remove(index);
    }

    void DestroyAllPreviewInstances()
    {
        _staleSlotKeys.Clear();
        foreach (KeyValuePair<int, PreviewSlot> pair in _slots)
            _staleSlotKeys.Add(pair.Key);

        for (int i = 0; i < _staleSlotKeys.Count; i++)
            DestroySlot(_staleSlotKeys[i]);

        ActionVfxEditorPreview.ResetTiming();
    }

    /// <summary>手动重播当前仍可见的全部 VFX 预览粒子。</summary>
    public void Replay()
    {
        // 强制下一 Tick 重新 SimulateAt（否则同帧会被跳过）。
        _lastSimulatedFrame = int.MinValue;

        foreach (KeyValuePair<int, PreviewSlot> pair in _slots)
        {
            if (pair.Value.Instance == null)
                continue;

            ActionVfxEditorPreview.RestartParticleSystems(pair.Value.Instance);
        }

        SceneView.RepaintAll();
    }
}

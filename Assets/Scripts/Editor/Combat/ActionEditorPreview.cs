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
        SampleRate = action != null ? action.SampleRate : 30f;
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
        && Action.AnimationClip != null;
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

/// <summary>解析 Preview Character 上 Hitbox / VFX 共用的 attachPoint。</summary>
public static class ActionEditorPreviewAttachPoint
{
    /// <summary>纯运行时不再挂载 HitBoxSystem / ActionVfxPlayer，预览默认使用 Preview Character 根节点。</summary>
    public static Transform Resolve(Transform previewCharacter)
    {
        return previewCharacter;
    }
}

/// <summary>Edit Mode 下用 AnimationMode 将 ActionDefinition 的 Clip 采样到 Preview Character。</summary>
public static class ActionEditorAnimationSampler
{
    static GameObject s_sampleRoot;
    static Animator s_animator;
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
        if (!TryResolveSampleRoot(previewCharacter, out GameObject sampleRoot, out Animator animator))
            return false;

        if (s_sampleRoot == sampleRoot && IsSessionActive)
            return true;

        EndSession();

        s_sampleRoot = sampleRoot;
        s_animator = animator;
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

        SceneView.RepaintAll();
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
    readonly Editor _owner;

    ActionDefinition _action;
    Transform _previewCharacter;
    int _previewFrame;
    int _lastSampledFrame = int.MinValue;
    AnimationClip _lastSampledClip;
    Transform _lastSampledCharacter;
    bool _extensionsBegun;

    public ActionEditorPreviewSession(Editor owner)
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

        if (NeedsResample())
        {
            ActionEditorAnimationSampler.Sample(
                context.Action.AnimationClip,
                context.PreviewTimeSeconds,
                context.SampleRate);

            _lastSampledFrame = _previewFrame;
            _lastSampledClip = context.Action.AnimationClip;
            _lastSampledCharacter = _previewCharacter;
        }

        BeginExtensionsIfNeeded(context);

        context = BuildContext();
        for (int i = 0; i < _extensions.Count; i++)
            _extensions[i].OnPreviewUpdate(in context);
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
        return _previewFrame != _lastSampledFrame
            || _action.AnimationClip != _lastSampledClip
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

/// <summary>VFX 帧事件 Scene 预览扩展：实例化 Prefab 并驱动 Edit Mode 粒子模拟。</summary>
public sealed class ActionEditorVfxPreviewExtension : IActionEditorPreviewExtension
{
    Func<SerializedProperty> _getSelectedVfxProp;

    GameObject _previewInstance;
    GameObject _previewSourcePrefab;

    /// <summary>关闭时不实例化 Prefab、不驱动粒子模拟。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>由 Editor 注入当前选中 VFX 的 SerializedProperty 读取器。</summary>
    public void Bind(Func<SerializedProperty> getSelectedVfxProp) => _getSelectedVfxProp = getSelectedVfxProp;

    /// <summary>关闭预览并立即销毁 Scene 中的临时实例。</summary>
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        if (!enabled)
            DestroyPreviewInstance();
    }

    public void OnPreviewBegin(in ActionEditorPreviewContext context) { }

    public void OnPreviewUpdate(in ActionEditorPreviewContext context)
    {
        if (!IsEnabled)
        {
            DestroyPreviewInstance();
            return;
        }

        SerializedProperty vfxProp = _getSelectedVfxProp?.Invoke();
        if (vfxProp == null || context.AttachPoint == null)
        {
            DestroyPreviewInstance();
            return;
        }

        UpdatePreviewInstance(vfxProp, context.AttachPoint);
        if (_previewInstance != null)
            ActionVfxEditorPreview.Simulate(_previewInstance);
    }

    public void OnPreviewEnd(in ActionEditorPreviewContext context) => DestroyPreviewInstance();

    void UpdatePreviewInstance(SerializedProperty vfxProp, Transform anchor)
    {
        SerializedProperty prefabProp = vfxProp.FindPropertyRelative("prefab");
        GameObject prefab = prefabProp != null ? prefabProp.objectReferenceValue as GameObject : null;

        if (prefab == null)
        {
            DestroyPreviewInstance();
            return;
        }

        if (_previewInstance == null || _previewSourcePrefab != prefab)
        {
            DestroyPreviewInstance();
            _previewSourcePrefab = prefab;
            _previewInstance = PrefabUtility.InstantiatePrefab(prefab, anchor) as GameObject;
            if (_previewInstance != null)
            {
                _previewInstance.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
                _previewInstance.name = $"[VFX Preview] {prefab.name}";
                ActionVfxEditorPreview.RestartParticleSystems(_previewInstance);
            }
        }

        if (_previewInstance == null)
            return;

        ApplyPreviewTransform(vfxProp, anchor);
    }

    void ApplyPreviewTransform(SerializedProperty vfxProp, Transform anchor)
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
            _previewInstance.transform.SetParent(anchor, false);
            _previewInstance.transform.localPosition = offsetProp.vector3Value;
            _previewInstance.transform.localRotation = Quaternion.Euler(eulerProp.vector3Value);
            _previewInstance.transform.localScale = safeScale;
            return;
        }

        _previewInstance.transform.SetParent(null, true);
        _previewInstance.transform.position = anchor.TransformPoint(offsetProp.vector3Value);
        _previewInstance.transform.rotation = anchor.rotation * Quaternion.Euler(eulerProp.vector3Value);
        _previewInstance.transform.localScale = safeScale;
    }

    void DestroyPreviewInstance()
    {
        if (_previewInstance != null)
        {
            UnityEngine.Object.DestroyImmediate(_previewInstance);
            _previewInstance = null;
        }

        _previewSourcePrefab = null;
        ActionVfxEditorPreview.ResetTiming();
    }

    /// <summary>手动重播当前 VFX 预览粒子。</summary>
    public void Replay()
    {
        if (_previewInstance != null)
            ActionVfxEditorPreview.RestartParticleSystems(_previewInstance);
        else
            SceneView.RepaintAll();
    }
}

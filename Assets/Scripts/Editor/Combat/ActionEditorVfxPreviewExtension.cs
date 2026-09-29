using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// VFX 帧事件 Scene 预览扩展：按预览帧驱动全部已触发条目的 Prefab/粒子，无需时间轴选中。
/// parentToAttachPoint：勾选跟随挂点；取消则在触发帧冻结世界空间（对齐运行时）。
/// </summary>
public sealed class ActionEditorVfxPreviewExtension : IActionEditorPreviewExtension
{
    /// <summary>单条 VFX 预览槽：缓存实例、源 Prefab，以及世界空间冻结位姿。</summary>
    sealed class PreviewSlot
    {
        public GameObject Instance;
        public GameObject SourcePrefab;
        public bool WorldSpaceFrozen;
        public Vector3 FrozenPosition;
        public Quaternion FrozenRotation;
        public Vector3 FrozenScale;
        public string ConfigFingerprint;
    }

    Func<SerializedProperty> _getVfxArrayProp;
    ActionEditorVfxWorldPoseEvaluator _worldPoseEvaluator;
    readonly Dictionary<int, PreviewSlot> _slots = new();
    readonly List<int> _staleSlotKeys = new();
    int _lastSimulatedFrame = int.MinValue;
    bool _lastEnabled;

    /// <summary>关闭时不实例化 Prefab、不驱动粒子模拟。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>由 Editor 注入 timeline.playVfxNotifies 数组属性读取器。</summary>
    public void Bind(Func<SerializedProperty> getVfxArrayProp) => _getVfxArrayProp = getVfxArrayProp;

    /// <summary>注入触发帧世界位姿评估（通常绑 ActionEditorPreviewSession）。</summary>
    public void BindWorldPoseEvaluator(ActionEditorVfxWorldPoseEvaluator evaluator) =>
        _worldPoseEvaluator = evaluator;

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

            SerializedProperty parentProp = vfxProp.FindPropertyRelative("parentToAttachPoint");
            // 缺省 true，与 PlayVfxNotify 默认一致
            bool parentToAttach = parentProp == null || parentProp.boolValue;

            PreviewSlot slot = EnsureSlot(i, prefab, anchor, parentToAttach);
            if (slot?.Instance == null)
                continue;

            alive.Add(i);
            ApplyPreviewTransform(slot, vfxProp, anchor, trigger, parentToAttach);

            if (!shouldResimulateParticles)
                continue;

            SerializedProperty speedProp = vfxProp.FindPropertyRelative("playbackSpeed");
            float localTime =
                ActionFrameQuery.GetElapsedSecondsSincePoint(trigger, context.PreviewFrame, sampleRate);
            float speed = speedProp != null ? Mathf.Max(0.0001f, speedProp.floatValue) : 1f;
            // 粒子 Simulate + Animator 同时间采样（对齐 showcase：playOnAwake + Animator）。
            ActionVfxEditorPreview.SampleAt(slot.Instance, localTime * speed);
        }

        DestroySlotsNotIn(alive);
        _lastSimulatedFrame = context.PreviewFrame;
        _lastEnabled = true;

        // 刚开启或帧变化时补刷新；稳态不再每帧 RepaintAll。
        if (shouldResimulateParticles && alive.Count > 0)
            SceneView.RepaintAll();
    }

    public void OnPreviewEnd(in ActionEditorPreviewContext context) => DestroyAllPreviewInstances();

    PreviewSlot EnsureSlot(int index, GameObject prefab, Transform anchor, bool parentToAttach)
    {
        if (_slots.TryGetValue(index, out PreviewSlot slot)
            && slot.Instance != null
            && slot.SourcePrefab == prefab)
            return slot;

        DestroySlot(index);

        // 世界空间预览：不要挂到角色下，否则会被动跟着动
        GameObject instance = parentToAttach
            ? PrefabUtility.InstantiatePrefab(prefab, anchor) as GameObject
            : PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            return null;

        instance.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
        instance.name = $"[VFX Preview {index}] {prefab.name}";
        ActionVfxEditorPreview.Restart(instance);

        slot = new PreviewSlot
        {
            Instance = instance,
            SourcePrefab = prefab,
        };
        _slots[index] = slot;
        return slot;
    }

    /// <summary>
    /// 跟随挂点，或在触发帧冻结世界空间（parentToAttachPoint=false，对齐 ActionVfxSpawner）。
    /// </summary>
    void ApplyPreviewTransform(
        PreviewSlot slot,
        SerializedProperty vfxProp,
        Transform anchor,
        int triggerFrame,
        bool parentToAttach)
    {
        SerializedProperty offsetProp = vfxProp.FindPropertyRelative("localOffset");
        SerializedProperty eulerProp = vfxProp.FindPropertyRelative("localEulerAngles");
        SerializedProperty scaleProp = vfxProp.FindPropertyRelative("localScale");
        SerializedProperty attachIdProp = vfxProp.FindPropertyRelative("attachPointId");

        if (offsetProp == null || eulerProp == null || scaleProp == null || slot?.Instance == null)
            return;

        Vector3 offset = offsetProp.vector3Value;
        Vector3 euler = eulerProp.vector3Value;
        Vector3 safeScale = Vector3.Max(scaleProp.vector3Value, Vector3.one * 0.01f);
        string attachId = attachIdProp != null ? attachIdProp.stringValue : string.Empty;
        string fingerprint = BuildConfigFingerprint(parentToAttach, attachId, offset, euler, safeScale, triggerFrame);

        if (parentToAttach)
        {
            slot.WorldSpaceFrozen = false;
            slot.ConfigFingerprint = fingerprint;
            slot.Instance.transform.SetParent(anchor, false);
            slot.Instance.transform.localPosition = offset;
            slot.Instance.transform.localRotation = Quaternion.Euler(euler);
            slot.Instance.transform.localScale = safeScale;
            return;
        }

        // 配置变更时重新冻结，避免改 Offset 后仍停在旧世界点
        if (!slot.WorldSpaceFrozen || slot.ConfigFingerprint != fingerprint)
        {
            if (_worldPoseEvaluator != null
                && _worldPoseEvaluator(
                    triggerFrame,
                    attachId,
                    offset,
                    euler,
                    out Vector3 worldPos,
                    out Quaternion worldRot))
            {
                slot.FrozenPosition = worldPos;
                slot.FrozenRotation = worldRot;
            }
            else
            {
                // 无评估器时退化为当前挂点（仍不再每帧跟随）
                slot.FrozenPosition = anchor.TransformPoint(offset);
                slot.FrozenRotation = anchor.rotation * Quaternion.Euler(euler);
            }

            slot.FrozenScale = safeScale;
            slot.WorldSpaceFrozen = true;
            slot.ConfigFingerprint = fingerprint;
        }

        slot.Instance.transform.SetParent(null, true);
        slot.Instance.transform.SetPositionAndRotation(slot.FrozenPosition, slot.FrozenRotation);
        slot.Instance.transform.localScale = slot.FrozenScale;
    }

    static string BuildConfigFingerprint(
        bool parentToAttach,
        string attachId,
        Vector3 offset,
        Vector3 euler,
        Vector3 scale,
        int triggerFrame) =>
        $"{parentToAttach}|{attachId}|{offset}|{euler}|{scale}|{triggerFrame}";

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

    /// <summary>手动重播当前仍可见的全部 VFX 预览（粒子 + Animator）。</summary>
    public void Replay()
    {
        // 强制下一 Tick 重新 SampleAt（否则同帧会被跳过）。
        _lastSimulatedFrame = int.MinValue;

        foreach (KeyValuePair<int, PreviewSlot> pair in _slots)
        {
            if (pair.Value.Instance == null)
                continue;

            ActionVfxEditorPreview.Restart(pair.Value.Instance);
        }

        SceneView.RepaintAll();
    }
}

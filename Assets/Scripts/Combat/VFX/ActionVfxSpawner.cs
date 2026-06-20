using UnityEngine;

/// <summary>按 ActionVfxKeyframe 配置实例化 VFX Prefab。</summary>
public static class ActionVfxSpawner
{
    /// <summary>生成 VFX 实例；优先经 VFXManager 对象池，无 Manager 时回退 Instantiate。</summary>
    public static GameObject Spawn(
        GameObject prefab,
        Transform root,
        Transform attachPoint,
        ActionVfxKeyframe vfx)
    {
        if (prefab == null || vfx == null)
            return null;

        Transform anchor = attachPoint != null ? attachPoint : root;
        if (anchor == null)
            return null;

        if (VFXManager.TryGetInstance(out VFXManager manager))
            return manager.Spawn(prefab, root, attachPoint, vfx);

        GameObject instance = Object.Instantiate(prefab);
        ApplyTransform(instance.transform, anchor, vfx);
        return instance;
    }

    /// <summary>将已存在的 Transform 对齐到 VFX 关键帧（Editor 预览复用）。</summary>
    public static void ApplyTransform(Transform instance, Transform anchor, ActionVfxKeyframe vfx)
    {
        if (instance == null || anchor == null || vfx == null)
            return;

        Vector3 safeScale = Vector3.Max(vfx.LocalScale, Vector3.one * 0.01f);

        if (vfx.ParentToAttachPoint)
        {
            instance.SetParent(anchor, false);
            instance.localPosition = vfx.LocalOffset;
            instance.localRotation = Quaternion.Euler(vfx.LocalEulerAngles);
            instance.localScale = safeScale;
            return;
        }

        instance.SetParent(null, true);
        instance.position = anchor.TransformPoint(vfx.LocalOffset);
        instance.rotation = anchor.rotation * Quaternion.Euler(vfx.LocalEulerAngles);
        instance.localScale = safeScale;
    }
}

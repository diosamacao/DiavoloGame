using System;
using UnityEngine;

/// <summary>角色模型的 Camera AnchorId 映射；模型层级变化只需在 Prefab 重绑此表。</summary>
[DisallowMultipleComponent]
public sealed class CameraAnchorProvider : AppControllerBase, ICameraAnchorProvider
{
    [Serializable]
    struct Entry
    {
        [SerializeField] string id;
        [SerializeField] Transform anchor;

        /// <summary>配置 Id。</summary>
        public string Id => id;

        /// <summary>模型上的实际锚点。</summary>
        public Transform Anchor => anchor;
    }

    [SerializeField] Entry[] entries = Array.Empty<Entry>();

    /// <inheritdoc />
    public bool TryResolveCameraAnchor(string anchorId, out Transform anchor)
    {
        anchor = null;
        if (string.IsNullOrWhiteSpace(anchorId))
            return false;

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].Id == anchorId && entries[i].Anchor != null)
            {
                anchor = entries[i].Anchor;
                return true;
            }
        }

        return false;
    }
}

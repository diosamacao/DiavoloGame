using UnityEngine;

/// <summary>按名称在模型层级下解析挂点；供 VFX / Hitbox 等共用，空 id 回退默认挂点。</summary>
public sealed class CharacterAttachPointResolver
{
    readonly Transform _searchRoot;
    readonly Transform _defaultAttach;

    /// <summary>默认挂点（通常为 Config.AttachPointName 解析结果）。</summary>
    public Transform DefaultAttach => _defaultAttach;

    /// <summary>搜索根（通常为 Model 实例根）。</summary>
    public Transform SearchRoot => _searchRoot;

    /// <summary>
    /// 创建解析器；searchRoot 为空时用 defaultAttach；defaultAttach 为空时用 searchRoot。
    /// </summary>
    public CharacterAttachPointResolver(Transform searchRoot, Transform defaultAttach)
    {
        _defaultAttach = defaultAttach != null ? defaultAttach : searchRoot;
        _searchRoot = searchRoot != null ? searchRoot : _defaultAttach;
    }

    /// <summary>按 attachPointId 查找子节点；空或找不到时返回默认挂点。</summary>
    public Transform Resolve(string attachPointId)
    {
        if (_defaultAttach == null)
            return null;

        if (string.IsNullOrWhiteSpace(attachPointId))
            return _defaultAttach;

        string name = attachPointId.Trim();
        Transform point = FindChildRecursive(_searchRoot, name);
        if (point != null)
            return point;

        Debug.LogWarning(
            $"CharacterAttachPointResolver: 找不到挂点「{name}」，已回退到默认挂点「{_defaultAttach.name}」。");
        return _defaultAttach;
    }

    /// <summary>仅按名称在指定根下查找，找不到返回 null（不打 Warning）。</summary>
    public static Transform FindByName(Transform searchRoot, string pointName)
    {
        if (searchRoot == null || string.IsNullOrWhiteSpace(pointName))
            return null;

        return FindChildRecursive(searchRoot, pointName.Trim());
    }

    static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        foreach (Transform child in root)
        {
            Transform match = FindChildRecursive(child, childName);
            if (match != null)
                return match;
        }

        return null;
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

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

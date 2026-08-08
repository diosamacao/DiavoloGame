using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Action Editor Hitbox 世界空间预览：parentToAttachPoint=false 时在 StartFrame 冻结 OBB。
/// 与运行时 HitboxFrameConsumer 语义对齐。
/// </summary>
public sealed class ActionEditorHitboxWorldSpacePreview
{
    struct FrozenEntry
    {
        public HitboxOrientedBox Box;
        public string Fingerprint;
    }

    readonly Dictionary<int, FrozenEntry> _frozen = new();
    readonly List<int> _staleKeys = new();

    /// <summary>换招式或关闭预览时清空冻结缓存。</summary>
    public void Clear() => _frozen.Clear();

    /// <summary>
    /// 解析当前预览帧应绘制的攻击盒。
    /// 跟随挂点：每帧重建；世界空间：首次激活时按 StartFrame 捕获并冻结。
    /// </summary>
    public HitboxOrientedBox ResolveBox(
        int hitboxIndex,
        HitboxNotifyState hitbox,
        Transform previewRoot,
        ActionEditorPreviewSession previewSession)
    {
        if (hitbox == null || previewRoot == null)
            return default;

        Transform anchor = ActionEditorPreviewAttachPoint.Resolve(previewRoot, hitbox.AttachPointId);
        string fingerprint = BuildFingerprint(hitbox);

        if (hitbox.ParentToAttachPoint)
        {
            _frozen.Remove(hitboxIndex);
            return HitboxMath.BuildFromHitbox(previewRoot, anchor, hitbox);
        }

        if (_frozen.TryGetValue(hitboxIndex, out FrozenEntry entry)
            && entry.Fingerprint == fingerprint)
        {
            return entry.Box;
        }

        HitboxOrientedBox captured = default;
        bool ok = previewSession != null
            && previewSession.TryEvaluateHitboxWorldBoxAtFrame(hitbox.StartFrame, hitbox, out captured);
        if (!ok)
            captured = HitboxMath.BuildFromHitbox(previewRoot, anchor, hitbox);

        _frozen[hitboxIndex] = new FrozenEntry
        {
            Box = captured,
            Fingerprint = fingerprint,
        };
        return captured;
    }

    /// <summary>移除当前帧未激活的冻结项，下次进入窗口重新捕获。</summary>
    public void PruneInactive(HitboxNotifyState[] hitboxes, int previewFrame)
    {
        if (_frozen.Count == 0)
            return;

        _staleKeys.Clear();
        foreach (KeyValuePair<int, FrozenEntry> pair in _frozen)
        {
            int index = pair.Key;
            if (hitboxes == null
                || index < 0
                || index >= hitboxes.Length
                || hitboxes[index] == null
                || !hitboxes[index].IsActiveAtFrame(previewFrame)
                || hitboxes[index].ParentToAttachPoint)
            {
                _staleKeys.Add(index);
            }
        }

        for (int i = 0; i < _staleKeys.Count; i++)
            _frozen.Remove(_staleKeys[i]);
    }

    static string BuildFingerprint(HitboxNotifyState hitbox) =>
        $"{hitbox.ParentToAttachPoint}|{hitbox.AttachPointId}|{hitbox.LocalOffset}|{hitbox.LocalEulerAngles}|{hitbox.Size}|{hitbox.StartFrame}";
}

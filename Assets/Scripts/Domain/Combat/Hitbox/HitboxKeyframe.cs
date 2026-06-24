using System;
using UnityEngine;

/// <summary>攻击判定框关键帧：帧区间内生效的局部 Box 形状。</summary>
[Serializable]
public class HitboxKeyframe
{
    [SerializeField] string hitboxId = "weapon_blade";
    [SerializeField] int startFrame;
    [SerializeField] int endFrame;
    [SerializeField] Vector3 localOffset = new(0f, 1f, 0.8f);
    [SerializeField] Vector3 localEulerAngles;
    [Tooltip("Box 全尺寸（与 Unity BoxCollider.size 一致），非半长。")]
    [SerializeField] Vector3 size = new(1.2f, 0.4f, 0.8f);

    public string HitboxId => string.IsNullOrEmpty(hitboxId) ? "default" : hitboxId;
    public int StartFrame => startFrame;
    public int EndFrame => endFrame;
    public Vector3 LocalOffset => localOffset;
    public Vector3 LocalEulerAngles => localEulerAngles;
    public Vector3 Size => size;

    /// <summary>指定帧是否落在此 Hitbox 生效区间内。</summary>
    public bool IsActiveAtFrame(int frame) =>
        endFrame >= startFrame && frame >= startFrame && frame <= endFrame;
}

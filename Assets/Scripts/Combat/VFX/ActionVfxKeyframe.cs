using System;
using UnityEngine;

/// <summary>招式 VFX 帧事件：在 triggerFrame 生成一次 Prefab，局部变换相对 attachPoint。</summary>
[Serializable]
public class ActionVfxKeyframe
{
    [SerializeField] string eventId = "slash_trail";
    [SerializeField] int triggerFrame;
    [SerializeField] GameObject prefab;
    [SerializeField] Vector3 localOffset = new(0f, 1f, 0.8f);
    [SerializeField] Vector3 localEulerAngles;
    [SerializeField] Vector3 localScale = Vector3.one;
    [Tooltip("勾选时实例挂到 attachPoint 下并使用 local 变换；否则在世界空间一次性生成。")]
    [SerializeField] bool parentToAttachPoint = true;

    public string EventId => string.IsNullOrEmpty(eventId) ? "vfx" : eventId;
    public int TriggerFrame => triggerFrame;
    public GameObject Prefab => prefab;
    public Vector3 LocalOffset => localOffset;
    public Vector3 LocalEulerAngles => localEulerAngles;
    public Vector3 LocalScale => localScale;
    public bool ParentToAttachPoint => parentToAttachPoint;

    /// <summary>从 previousFrame 推进到 currentFrame 时是否应触发（含跨帧补偿）。</summary>
    public bool ShouldFireBetweenFrames(int previousFrame, int currentFrame) =>
        triggerFrame > previousFrame && triggerFrame <= currentFrame;

    /// <summary>将 triggerFrame 限制在 [0, totalFrames - 1]。</summary>
    public void ClampToTotalFrames(int totalFrames)
    {
        int maxFrame = Mathf.Max(0, totalFrames - 1);
        triggerFrame = Mathf.Clamp(triggerFrame, 0, maxFrame);
    }
}

using System;
using UnityEngine;

/// <summary>时间轴事件骨架：triggerFrame 触发；完整运行时派发待 ActionEditor M5 接入。</summary>
[Serializable]
public class ActionEvent
{
    [SerializeField] string eventId = "event";
    [SerializeField] ActionEventKind kind = ActionEventKind.Custom;
    [SerializeField] int triggerFrame;
    [SerializeField] int endFrame = -1;
    [SerializeField] string payloadId = string.Empty;

    public string EventId => string.IsNullOrEmpty(eventId) ? "event" : eventId;
    public ActionEventKind Kind => kind;
    public int TriggerFrame => triggerFrame;
    public int EndFrame => endFrame;
    public string PayloadId => payloadId;

    /// <summary>从 previousFrame 推进到 currentFrame 时是否应触发（含跨帧补偿）。</summary>
    public bool ShouldFireBetweenFrames(int previousFrame, int currentFrame) =>
        triggerFrame > previousFrame && triggerFrame <= currentFrame;

    /// <summary>将 triggerFrame / endFrame 限制在有效帧范围内。</summary>
    public void ClampToTotalFrames(int totalFrames)
    {
        int maxFrame = Mathf.Max(0, totalFrames - 1);
        triggerFrame = Mathf.Clamp(triggerFrame, 0, maxFrame);
        if (endFrame >= 0)
            endFrame = Mathf.Clamp(endFrame, triggerFrame, maxFrame);
    }
}

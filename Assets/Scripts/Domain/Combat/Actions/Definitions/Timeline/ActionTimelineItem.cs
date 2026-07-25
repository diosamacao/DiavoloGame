using System;
using UnityEngine;

/// <summary>动作时间轴条目的共同数据：帧区间、轨道、优先级与编辑器可识别 id。</summary>
[Serializable]
public abstract class ActionTimelineItem
{
    [SerializeField] string id = "timeline_item";
    [SerializeField] int startFrame;
    [SerializeField] int endFrame;
    [SerializeField] int priority = 0;
    [SerializeField] string trackName = "Default";

    /// <summary>编辑器和运行时日志使用的稳定条目标识。</summary>
    public string Id => string.IsNullOrEmpty(id) ? GetType().Name : id;

    /// <summary>写入稳定时间轴条目 id；已有非默认 id 时不会被空串覆盖。</summary>
    public void SetId(string newId)
    {
        if (!string.IsNullOrEmpty(newId))
            id = newId;
    }

    /// <summary>条目开始逻辑帧，点事件同时作为触发帧。</summary>
    public int StartFrame => startFrame;

    /// <summary>条目结束逻辑帧；点事件会折叠为 StartFrame。</summary>
    public int EndFrame => endFrame;

    /// <summary>同帧触发顺序，数值越大越先处理。</summary>
    public int Priority => priority;

    /// <summary>编辑器时间轴轨道名。</summary>
    public string TrackName => string.IsNullOrEmpty(trackName) ? "Default" : trackName;

    /// <summary>指定帧是否落在该条目的闭区间内。</summary>
    public virtual bool IsActiveAtFrame(int frame) =>
        endFrame >= startFrame && frame >= startFrame && frame <= endFrame;

    /// <summary>将条目帧区间限制到动作总帧数内，供 ActionDefinition.OnValidate 统一调用。</summary>
    public virtual void ClampToTotalFrames(int totalFrames)
    {
        int maxFrame = Mathf.Max(0, totalFrames - 1);
        startFrame = Mathf.Clamp(startFrame, 0, maxFrame);
        endFrame = Mathf.Clamp(endFrame, startFrame, maxFrame);
    }

    /// <summary>点事件使用单帧区间时调用，保证 startFrame == endFrame。</summary>
    protected void CollapseToStartFrame()
    {
        endFrame = startFrame;
    }
}

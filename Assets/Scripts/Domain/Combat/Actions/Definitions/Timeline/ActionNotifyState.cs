using System;

/// <summary>动作时间轴区间窗口基类；用于 Hitbox、Hurtbox、VFX、SFX、Cancel、Movement、Rotation 等持续帧窗口。</summary>
[Serializable]
public class ActionNotifyState : ActionTimelineItem
{
    /// <summary>上一帧不在区间、当前帧进入区间时调用。</summary>
    public virtual void OnEnter(in ActionNotifyContext context) { }

    /// <summary>当前帧处于区间内时调用，每个逻辑帧最多一次。</summary>
    public virtual void OnTick(in ActionNotifyContext context) { }

    /// <summary>上一帧在区间、当前帧离开区间时调用。</summary>
    public virtual void OnExit(in ActionNotifyContext context) { }

    /// <summary>当前推进是否进入该区间。</summary>
    public bool ShouldEnter(int previousFrame, int currentFrame) =>
        !IsActiveAtFrame(previousFrame) && IsActiveAtFrame(currentFrame);

    /// <summary>当前推进是否离开该区间。</summary>
    public bool ShouldExit(int previousFrame, int currentFrame) =>
        IsActiveAtFrame(previousFrame) && !IsActiveAtFrame(currentFrame);
}

using System;

/// <summary>动作时间轴点事件基类；用于 CameraShake、自定义信号等单帧触发（VFX/SFX 为区间窗口）。</summary>
[Serializable]
public class ActionNotify : ActionTimelineItem
{
    /// <summary>点事件触发帧，等同于 StartFrame。</summary>
    public int TriggerFrame => StartFrame;

    /// <summary>从 previousFrame 推进到 currentFrame 时是否应触发，支持低帧率跨帧补偿。</summary>
    public bool ShouldFireBetweenFrames(int previousFrame, int currentFrame) =>
        TriggerFrame > previousFrame && TriggerFrame <= currentFrame;

    /// <summary>点事件运行时触发钩子；数据类默认不执行副作用，由 Consumer 解释。</summary>
    public virtual void OnNotify(in ActionNotifyContext context) { }

    /// <summary>点事件总是折叠为单帧，避免编辑器拖拽后出现隐式区间。</summary>
    public override void ClampToTotalFrames(int totalFrames)
    {
        base.ClampToTotalFrames(totalFrames);
        CollapseToStartFrame();
    }
}

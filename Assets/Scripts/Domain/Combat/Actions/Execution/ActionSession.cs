/// <summary>单个角色当前招式会话；是 Action 是否激活、当前帧与命中状态的唯一权威。</summary>
public sealed class ActionSession
{
    /// <summary>当前正在播放的招式；为空表示没有激活招式。</summary>
    public ActionDefinition CurrentAction { get; private set; }

    /// <summary>当前招式是否激活。</summary>
    public bool IsActive => CurrentAction != null;

    /// <summary>当前招式已推进秒数。</summary>
    public float ElapsedSeconds { get; private set; }

    /// <summary>上一次派发过的逻辑帧，用于避免大 delta 漏帧。</summary>
    public int LastProcessedFrame { get; set; } = -1;

    /// <summary>当前已切入的动画段索引；用于段边界 PlayClip 去重。</summary>
    public int CurrentAnimationSegmentIndex { get; set; } = -1;

    /// <summary>卡肉期间暂停招式逻辑时间。</summary>
    public bool IsHitStopPaused { get; private set; }

    /// <summary>本招是否已经发生命中确认。</summary>
    public bool HasConfirmedHit { get; private set; }

    bool _hitStopTriggered;

    /// <summary>开始一个新招式会话，并清空上一个会话的派生状态。</summary>
    public void Begin(ActionDefinition action)
    {
        CurrentAction = action;
        ElapsedSeconds = 0f;
        LastProcessedFrame = -1;
        CurrentAnimationSegmentIndex = -1;
        IsHitStopPaused = false;
        HasConfirmedHit = false;
        _hitStopTriggered = false;
    }

    /// <summary>结束当前招式会话。</summary>
    public void Stop()
    {
        CurrentAction = null;
        ElapsedSeconds = 0f;
        LastProcessedFrame = -1;
        CurrentAnimationSegmentIndex = -1;
        IsHitStopPaused = false;
        HasConfirmedHit = false;
        _hitStopTriggered = false;
    }

    /// <summary>推进会话时间；调用方保证会话激活且未暂停。</summary>
    public void Advance(float deltaTime)
    {
        ElapsedSeconds += deltaTime;
    }

    /// <summary>设置编辑器 Scrub 对应的逻辑时间。</summary>
    public void SetFrame(int frameIndex)
    {
        if (CurrentAction == null)
            return;

        ElapsedSeconds = frameIndex / CurrentAction.SampleRate;
    }

    /// <summary>设置卡肉暂停状态。</summary>
    public void SetHitStopPaused(bool paused)
    {
        IsHitStopPaused = paused;
    }

    /// <summary>标记本招已经命中，用于 OnHitConfirm / OnWhiff。</summary>
    public void ConfirmHit()
    {
        HasConfirmedHit = true;
    }

    /// <summary>每招仅允许消费一次卡肉触发。</summary>
    public bool TryConsumeHitStopTrigger()
    {
        if (_hitStopTriggered)
            return false;

        _hitStopTriggered = true;
        return true;
    }
}

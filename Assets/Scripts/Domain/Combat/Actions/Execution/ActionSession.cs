/// <summary>单个角色当前招式会话；是 Action 是否激活、当前帧、图游标与命中状态的唯一权威。</summary>
public sealed class ActionSession
{
    ActionFrameClock _frameClock;

    /// <summary>当前正在播放的招式；为空表示没有激活招式。</summary>
    public ActionDefinition CurrentAction { get; private set; }

    /// <summary>当前招式是否激活。</summary>
    public bool IsActive => CurrentAction != null;

    /// <summary>当前整数动作帧；等于 TotalFrames 时表示动作完整时长已经结束。</summary>
    public int CurrentFrame => _frameClock.CurrentFrame;

    /// <summary>仅供动画与表现读取的派生秒数；不参与逻辑判断。</summary>
    public float ElapsedSeconds =>
        CurrentAction != null ? CurrentFrame / (float)CurrentAction.SampleRate : 0f;

    /// <summary>当前会话是否已经推进完全部动作帧。</summary>
    public bool IsComplete =>
        CurrentAction != null && CurrentFrame >= CurrentAction.TotalFrames;

    /// <summary>上一次派发过的动作帧，用于稳定补发跨过的采样帧。</summary>
    public int LastProcessedFrame { get; set; } = -1;

    /// <summary>当前已切入的动画段索引；用于段边界 PlayClip 去重。</summary>
    public int CurrentAnimationSegmentIndex { get; set; } = -1;

    /// <summary>本招是否已经发生命中确认。</summary>
    public bool HasConfirmedHit { get; private set; }

    /// <summary>当前招式会话编号；同一角色内单调递增，停止时为 0。</summary>
    public int InstanceId { get; private set; }

    /// <summary>当前连招图；不在图内时为 null。</summary>
    public ActionGraph CurrentGraph { get; private set; }

    /// <summary>当前图节点 id；不在图内时为 null。</summary>
    public string CurrentNodeId { get; private set; }

    /// <summary>是否处于连招图游标中。</summary>
    public bool HasGraphCursor => CurrentGraph != null && !string.IsNullOrEmpty(CurrentNodeId);

    /// <summary>当前图游标对应节点；直接播放动作时返回 false。</summary>
    public bool TryGetCurrentNode(out ActionGraphNode node)
    {
        node = null;
        return HasGraphCursor && CurrentGraph.TryGetNode(CurrentNodeId, out node);
    }

    int _nextInstanceId;

    /// <summary>开始一个新招式会话，并清空上一个会话的派生状态（含图游标）。</summary>
    public void Begin(ActionDefinition action)
    {
        CurrentAction = action;
        _frameClock.Reset();
        LastProcessedFrame = -1;
        CurrentAnimationSegmentIndex = -1;
        HasConfirmedHit = false;
        _nextInstanceId = _nextInstanceId == int.MaxValue ? 1 : _nextInstanceId + 1;
        InstanceId = _nextInstanceId;
        ClearGraphCursor();
    }

    /// <summary>在 Begin 之后写入图游标（由 Graph 解析结果应用）。</summary>
    public void SetGraphCursor(ActionGraph graph, string nodeId)
    {
        CurrentGraph = graph;
        CurrentNodeId = nodeId;
    }

    /// <summary>离开连招图（例如取消进闪避）。</summary>
    public void ClearGraphCursor()
    {
        CurrentGraph = null;
        CurrentNodeId = null;
    }

    /// <summary>结束当前招式会话。</summary>
    public void Stop()
    {
        CurrentAction = null;
        _frameClock.Reset();
        LastProcessedFrame = -1;
        CurrentAnimationSegmentIndex = -1;
        HasConfirmedHit = false;
        InstanceId = 0;
        ClearGraphCursor();
    }

    /// <summary>推进一个固定模拟帧；返回本次跨过的动作采样帧数。</summary>
    public int AdvanceFrame(int simulationRate)
    {
        if (CurrentAction == null)
            return 0;

        return _frameClock.Advance(
            CurrentAction.SampleRate,
            simulationRate,
            CurrentAction.TotalFrames);
    }

    /// <summary>标记本招已经命中，用于 OnHitConfirm / OnWhiff。</summary>
    public void ConfirmHit()
    {
        HasConfirmedHit = true;
    }

}

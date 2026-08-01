/// <summary>ActionSim 对外输出的确定性逻辑事件类型。</summary>
public enum ActionSimEventType
{
    /// <summary>动作实例已在 frame 0 开始。</summary>
    Started = 0,

    /// <summary>动作权威帧已跨越并可供表现或战斗层采样。</summary>
    FrameAdvanced = 1,

    /// <summary>当前动作实例已收到有效命中确认。</summary>
    HitConfirmed = 2,

    /// <summary>动作实例因停止、自然结束或切招而结束。</summary>
    Stopped = 3,
}

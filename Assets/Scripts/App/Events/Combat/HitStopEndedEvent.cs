/// <summary>卡肉结束事件；表现系统收到后恢复自身时间。</summary>
public readonly struct HitStopEndedEvent : IArchitectureEvent
{
    /// <summary>共享的无载荷事件实例。</summary>
    public static readonly HitStopEndedEvent Instance = new();
}

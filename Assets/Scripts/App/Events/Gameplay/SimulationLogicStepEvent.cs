/// <summary>每完成一个权威逻辑帧后发布；供表现层按帧倒计时（如 HitStop VFX）。</summary>
public readonly struct SimulationLogicStepEvent : IArchitectureEvent
{
    /// <summary>无载荷单例。</summary>
    public static readonly SimulationLogicStepEvent Instance = new();
}

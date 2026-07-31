/// <summary>由 SimulationWorld 按稳定顺序逐逻辑帧推进的 Actor 契约。</summary>
public interface ISimulationActor
{
    /// <summary>推进一个固定逻辑帧；实现不得读取 Unity Time。</summary>
    void Step(long frameIndex, float fixedDeltaSeconds);
}

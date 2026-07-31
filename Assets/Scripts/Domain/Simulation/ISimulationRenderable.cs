/// <summary>将固定帧状态插值到当前渲染帧的可选 Actor 契约。</summary>
public interface ISimulationRenderable
{
    /// <summary>按当前 accumulator 比例更新非权威表现，不得修改模拟状态。</summary>
    void Render(float interpolationAlpha);
}

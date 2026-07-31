/// <summary>固定逻辑帧之间需要先汇聚渲染帧输入边沿的可选 Actor 契约。</summary>
public interface IRenderFrameSampler
{
    /// <summary>每个 Unity 渲染帧调用一次；实现只缓存输入，不推进模拟状态。</summary>
    void SampleRenderFrame();
}

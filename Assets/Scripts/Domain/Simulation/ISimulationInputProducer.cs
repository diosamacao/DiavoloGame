/// <summary>在 Actor 状态推进前，为当前逻辑帧生成量化输入的可选参与者。</summary>
public interface ISimulationInputProducer
{
    /// <summary>基于上一帧已提交状态生成当前帧输入；不得推进角色玩法状态。</summary>
    void ProduceInput(long frameIndex);
}

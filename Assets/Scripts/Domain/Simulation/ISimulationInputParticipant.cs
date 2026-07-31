/// <summary>需要获知自身 SimActorId 并访问 World 输入历史的模拟参与者。</summary>
public interface ISimulationInputParticipant
{
    /// <summary>注册时绑定会话内稳定身份与输入缓冲；同一实例只绑定一次。</summary>
    void BindSimulationInput(SimActorId actorId, InputFrameBuffer inputFrames);
}

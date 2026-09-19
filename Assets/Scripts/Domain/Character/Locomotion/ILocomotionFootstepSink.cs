/// <summary>只读消费逻辑 FootCycle 落脚边沿的表现端口。</summary>
public interface ILocomotionFootstepSink
{
    /// <summary>若本帧产生落脚边沿则播放对应反馈。</summary>
    void PlayIfPlanted(FootSide? planted);
}

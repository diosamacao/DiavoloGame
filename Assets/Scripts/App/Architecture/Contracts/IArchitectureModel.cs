/// <summary>架构级数据模型接口；Model 只保存共享状态，不承载复杂业务流程。</summary>
public interface IArchitectureModel :
    IBelongToArchitecture,
    ICanSetArchitecture,
    ICanGetUtility,
    ICanSendEvent
{
    /// <summary>由架构入口调用，绑定所属架构并初始化模型状态。</summary>
    void Initialize(ACTGameArchitecture architecture);

    /// <summary>由架构入口反初始化时调用，释放模型持有的运行时资源。</summary>
    void Deinitialize();
}

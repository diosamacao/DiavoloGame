/// <summary>架构级业务系统接口；只有实现该契约的对象才能注册进 ACTGameArchitecture IOC。</summary>
public interface IArchitectureSystem :
    IBelongToArchitecture,
    ICanSetArchitecture,
    ICanGetSystem,
    ICanGetModel,
    ICanGetUtility,
    ICanRegisterEvent,
    ICanSendEvent
{
    /// <summary>由架构入口调用，绑定所属架构并执行系统初始化。</summary>
    void Initialize(ACTGameArchitecture architecture);

    /// <summary>由架构入口反初始化时调用，释放事件订阅或运行时缓存。</summary>
    void Deinitialize();
}

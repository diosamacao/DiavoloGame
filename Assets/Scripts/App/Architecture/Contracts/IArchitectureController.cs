/// <summary>架构表现层控制器接口；Unity 入口脚本通过该契约访问 Command、Query 与 Event。</summary>
public interface IArchitectureController :
    IBelongToArchitecture,
    ICanGetSystem,
    ICanGetModel,
    ICanGetUtility,
    ICanSendCommand,
    ICanSendQuery,
    ICanRegisterEvent
{
}

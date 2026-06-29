/// <summary>架构 Command 基类；命令通过能力接口访问 System、Model 并发送 Event。</summary>
public abstract class ArchitectureCommandBase :
    ArchitectureElementBase,
    IArchitectureCommand,
    ICanGetSystem,
    ICanGetModel,
    ICanGetUtility,
    ICanSendEvent
{
    /// <summary>绑定架构后执行命令；命令实例只用于本次调用。</summary>
    public void Execute(ACTGameArchitecture architecture)
    {
        SetArchitecture(architecture);
        OnExecute();
    }

    /// <summary>子类实现一次业务行为。</summary>
    protected abstract void OnExecute();
}

/// <summary>架构命令接口；用于表达一次会改变状态或产生副作用的业务行为。</summary>
public interface IArchitectureCommand
{
    /// <summary>执行命令；命令可访问系统、模型并发送事件。</summary>
    void Execute(ACTGameArchitecture architecture);
}

/// <summary>架构命令接口；用于表达一次会改变状态或产生副作用的业务行为。</summary>
public interface IArchitectureCommand
{
    /// <summary>执行命令；命令对象只携带本次执行上下文，不保存跨帧可变状态。</summary>
    void Execute(ACTGameArchitecture architecture);
}

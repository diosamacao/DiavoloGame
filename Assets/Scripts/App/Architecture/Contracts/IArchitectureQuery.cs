/// <summary>架构查询接口；用于表达不会修改状态的读取请求。</summary>
public interface IArchitectureQuery<TResult>
{
    /// <summary>执行查询并返回结果；查询不得写入 System/Model 状态或发送事件。</summary>
    TResult Execute(ACTGameArchitecture architecture);
}

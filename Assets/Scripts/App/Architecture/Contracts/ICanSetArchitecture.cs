using System;

/// <summary>声明对象允许由架构入口注入所属 Architecture。</summary>
public interface ICanSetArchitecture
{
    /// <summary>绑定当前对象所属的架构入口。</summary>
    void SetArchitecture(ACTGameArchitecture architecture);
}

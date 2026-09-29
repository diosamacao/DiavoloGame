using System;

/// <summary>声明对象归属于某个 ACTGameArchitecture 实例。</summary>
public interface IBelongToArchitecture
{
    /// <summary>返回当前对象所属的架构入口。</summary>
    ACTGameArchitecture GetArchitecture();
}

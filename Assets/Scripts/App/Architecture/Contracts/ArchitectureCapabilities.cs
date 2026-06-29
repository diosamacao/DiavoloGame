using System;

/// <summary>声明对象归属于某个 ACTGameArchitecture 实例。</summary>
public interface IBelongToArchitecture
{
    /// <summary>返回当前对象所属的架构入口。</summary>
    ACTGameArchitecture GetArchitecture();
}

/// <summary>声明对象允许由架构入口注入所属 Architecture。</summary>
public interface ICanSetArchitecture
{
    /// <summary>绑定当前对象所属的架构入口。</summary>
    void SetArchitecture(ACTGameArchitecture architecture);
}

/// <summary>声明对象可读取架构级 System。</summary>
public interface ICanGetSystem : IBelongToArchitecture { }

/// <summary>声明对象可读取架构级 Model。</summary>
public interface ICanGetModel : IBelongToArchitecture { }

/// <summary>声明对象可读取架构级 Utility。</summary>
public interface ICanGetUtility : IBelongToArchitecture { }

/// <summary>声明对象可发送会改变状态的 Command。</summary>
public interface ICanSendCommand : IBelongToArchitecture { }

/// <summary>声明对象可发送无副作用 Query。</summary>
public interface ICanSendQuery : IBelongToArchitecture { }

/// <summary>声明对象可注册或注销架构事件。</summary>
public interface ICanRegisterEvent : IBelongToArchitecture { }

/// <summary>声明对象可广播架构事件。</summary>
public interface ICanSendEvent : IBelongToArchitecture { }

/// <summary>QFramework 风格能力接口的扩展方法，集中转发到所属 Architecture。</summary>
public static class ArchitectureCapabilityExtensions
{
    /// <summary>通过能力接口获取已注册 System。</summary>
    public static TSystem GetSystem<TSystem>(this ICanGetSystem self)
        where TSystem : class, IArchitectureSystem
    {
        return self.GetArchitecture().GetSystem<TSystem>();
    }

    /// <summary>通过能力接口获取已注册 Model。</summary>
    public static TModel GetModel<TModel>(this ICanGetModel self)
        where TModel : class, IArchitectureModel
    {
        return self.GetArchitecture().GetModel<TModel>();
    }

    /// <summary>通过能力接口获取已注册 Utility。</summary>
    public static TUtility GetUtility<TUtility>(this ICanGetUtility self)
        where TUtility : class, IArchitectureUtility
    {
        return self.GetArchitecture().GetUtility<TUtility>();
    }

    /// <summary>通过能力接口发送会改变状态的 Command。</summary>
    public static void SendCommand(this ICanSendCommand self, IArchitectureCommand command)
    {
        self.GetArchitecture().SendCommand(command);
    }

    /// <summary>通过能力接口发送无副作用 Query。</summary>
    public static TResult SendQuery<TResult>(this ICanSendQuery self, IArchitectureQuery<TResult> query)
    {
        return self.GetArchitecture().SendQuery(query);
    }

    /// <summary>通过能力接口订阅架构事件。</summary>
    public static void RegisterEvent<TEvent>(this ICanRegisterEvent self, Action<TEvent> handler)
        where TEvent : IArchitectureEvent
    {
        self.GetArchitecture().RegisterEvent(handler);
    }

    /// <summary>通过能力接口取消订阅架构事件。</summary>
    public static void UnregisterEvent<TEvent>(this ICanRegisterEvent self, Action<TEvent> handler)
        where TEvent : IArchitectureEvent
    {
        self.GetArchitecture().UnregisterEvent(handler);
    }

    /// <summary>通过能力接口广播架构事件。</summary>
    public static void SendEvent<TEvent>(this ICanSendEvent self, TEvent architectureEvent)
        where TEvent : IArchitectureEvent
    {
        self.GetArchitecture().SendEvent(architectureEvent);
    }
}

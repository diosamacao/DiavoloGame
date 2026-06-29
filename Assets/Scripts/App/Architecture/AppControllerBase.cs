using System;
using UnityEngine;

/// <summary>Unity 表现层控制器基类；Controller 通过受控能力访问架构层。</summary>
public abstract class AppControllerBase : MonoBehaviour, IArchitectureController
{
    /// <summary>返回当前项目的全局架构入口。</summary>
    public ACTGameArchitecture GetArchitecture()
    {
        return ACTGameArchitecture.Interface;
    }

    /// <summary>获取已注册的架构 System。</summary>
    protected TSystem GetSystem<TSystem>()
        where TSystem : class, IArchitectureSystem
    {
        return GetArchitecture().GetSystem<TSystem>();
    }

    /// <summary>获取已注册的架构 Model。</summary>
    protected TModel GetModel<TModel>()
        where TModel : class, IArchitectureModel
    {
        return GetArchitecture().GetModel<TModel>();
    }

    /// <summary>获取已注册的架构 Utility。</summary>
    protected TUtility GetUtility<TUtility>()
        where TUtility : class, IArchitectureUtility
    {
        return GetArchitecture().GetUtility<TUtility>();
    }

    /// <summary>发送一次会改变状态的 Command。</summary>
    protected void SendCommand(IArchitectureCommand command)
    {
        GetArchitecture().SendCommand(command);
    }

    /// <summary>发送一次无副作用 Query。</summary>
    protected TResult SendQuery<TResult>(IArchitectureQuery<TResult> query)
    {
        return GetArchitecture().SendQuery(query);
    }

    /// <summary>订阅架构事件；通常在 OnEnable 调用。</summary>
    protected void RegisterEvent<TEvent>(Action<TEvent> handler)
        where TEvent : IArchitectureEvent
    {
        GetArchitecture().RegisterEvent(handler);
    }

    /// <summary>取消订阅架构事件；通常在 OnDisable 调用。</summary>
    protected void UnregisterEvent<TEvent>(Action<TEvent> handler)
        where TEvent : IArchitectureEvent
    {
        GetArchitecture().UnregisterEvent(handler);
    }
}

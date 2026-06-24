using System;
using System.Collections.Generic;

/// <summary>项目级架构入口，提供 System 注册、Command 执行、Query 查询与 Event 分发。</summary>
public sealed class ACTGameArchitecture
{
    static ACTGameArchitecture s_interface;

    readonly Dictionary<Type, object> _systems = new();
    readonly Dictionary<Type, Delegate> _eventHandlers = new();

    /// <summary>全局架构入口；首次访问时完成系统注册。</summary>
    public static ACTGameArchitecture Interface
    {
        get
        {
            if (s_interface == null)
                s_interface = CreateDefault();

            return s_interface;
        }
    }

    ACTGameArchitecture() { }

    /// <summary>获取已注册系统。</summary>
    public TSystem GetSystem<TSystem>() where TSystem : class
    {
        if (_systems.TryGetValue(typeof(TSystem), out object system))
            return system as TSystem;

        return null;
    }

    /// <summary>执行一次业务命令。</summary>
    public void SendCommand(IArchitectureCommand command)
    {
        command?.Execute(this);
    }

    /// <summary>执行无副作用查询并返回结果。</summary>
    public TResult SendQuery<TResult>(IArchitectureQuery<TResult> query)
    {
        return query != null ? query.Execute(this) : default;
    }

    /// <summary>订阅指定事件类型。</summary>
    public void RegisterEvent<TEvent>(Action<TEvent> handler)
    {
        if (handler == null)
            return;

        Type eventType = typeof(TEvent);
        _eventHandlers.TryGetValue(eventType, out Delegate existing);
        _eventHandlers[eventType] = Delegate.Combine(existing, handler);
    }

    /// <summary>取消订阅指定事件类型。</summary>
    public void UnregisterEvent<TEvent>(Action<TEvent> handler)
    {
        if (handler == null)
            return;

        Type eventType = typeof(TEvent);
        if (!_eventHandlers.TryGetValue(eventType, out Delegate existing))
            return;

        Delegate updated = Delegate.Remove(existing, handler);
        if (updated == null)
            _eventHandlers.Remove(eventType);
        else
            _eventHandlers[eventType] = updated;
    }

    /// <summary>向全部订阅者分发事件。</summary>
    public void SendEvent<TEvent>(TEvent architectureEvent)
    {
        if (_eventHandlers.TryGetValue(typeof(TEvent), out Delegate handlers)
            && handlers is Action<TEvent> typedHandlers)
        {
            typedHandlers.Invoke(architectureEvent);
        }
    }

    void RegisterSystem<TSystem>(TSystem system) where TSystem : class, IArchitectureSystem
    {
        _systems[typeof(TSystem)] = system;
        system.Initialize(this);
    }

    static ACTGameArchitecture CreateDefault()
    {
        var architecture = new ACTGameArchitecture();
        architecture.RegisterSystem(new CombatActorSystem());
        architecture.RegisterSystem(new TargetSystem());
        architecture.RegisterSystem(new CombatFeedbackSystem());
        return architecture;
    }
}

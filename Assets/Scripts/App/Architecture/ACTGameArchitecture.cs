using System;
using System.Collections.Generic;

/// <summary>项目级架构入口，提供 System/Model/Utility 注册、Command 执行、Query 查询与 Event 分发。</summary>
public sealed class ACTGameArchitecture
{
    static ACTGameArchitecture s_interface;

    readonly Dictionary<Type, IArchitectureSystem> _systems = new();
    readonly Dictionary<Type, IArchitectureModel> _models = new();
    readonly Dictionary<Type, IArchitectureUtility> _utilities = new();
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

    /// <summary>获取已注册系统；只有 IArchitectureSystem 类型能从 IOC 取出。</summary>
    public TSystem GetSystem<TSystem>() where TSystem : class, IArchitectureSystem
    {
        if (_systems.TryGetValue(typeof(TSystem), out IArchitectureSystem system))
            return system as TSystem;

        return null;
    }

    /// <summary>获取已注册模型；Model 只保存共享状态。</summary>
    public TModel GetModel<TModel>() where TModel : class, IArchitectureModel
    {
        if (_models.TryGetValue(typeof(TModel), out IArchitectureModel model))
            return model as TModel;

        return null;
    }

    /// <summary>获取已注册工具；Utility 封装外部服务或无状态适配能力。</summary>
    public TUtility GetUtility<TUtility>() where TUtility : class, IArchitectureUtility
    {
        if (_utilities.TryGetValue(typeof(TUtility), out IArchitectureUtility utility))
            return utility as TUtility;

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
        where TEvent : IArchitectureEvent
    {
        if (handler == null)
            return;

        Type eventType = typeof(TEvent);
        _eventHandlers.TryGetValue(eventType, out Delegate existing);
        _eventHandlers[eventType] = Delegate.Combine(existing, handler);
    }

    /// <summary>取消订阅指定事件类型。</summary>
    public void UnregisterEvent<TEvent>(Action<TEvent> handler)
        where TEvent : IArchitectureEvent
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
        where TEvent : IArchitectureEvent
    {
        if (_eventHandlers.TryGetValue(typeof(TEvent), out Delegate handlers)
            && handlers is Action<TEvent> typedHandlers)
        {
            typedHandlers.Invoke(architectureEvent);
        }
    }

    void RegisterSystem<TSystem>(TSystem system) where TSystem : class, IArchitectureSystem
    {
        if (system == null)
            return;

        _systems[typeof(TSystem)] = system;
        system.Initialize(this);
    }

    void RegisterModel<TModel>(TModel model) where TModel : class, IArchitectureModel
    {
        if (model == null)
            return;

        _models[typeof(TModel)] = model;
        model.Initialize(this);
    }

    void RegisterUtility<TUtility>(TUtility utility) where TUtility : class, IArchitectureUtility
    {
        if (utility == null)
            return;

        _utilities[typeof(TUtility)] = utility;
    }

    static ACTGameArchitecture CreateDefault()
    {
        var architecture = new ACTGameArchitecture();
        architecture.RegisterSystem(new CombatActorSystem());
        architecture.RegisterSystem(new TargetSystem());
        architecture.RegisterSystem(new CombatFeedbackSystem());
        architecture.RegisterSystem(new EnemySpawnSystem());
        architecture.RegisterSystem(new LocalPlayerService());
        return architecture;
    }
}

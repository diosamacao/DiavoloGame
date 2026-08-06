using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>场景中唯一的 Unity 时间入口，把渲染帧时间转换为固定 60Hz SimulationWorld Step。</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class SimulationHost : AppControllerBase
{
    readonly Dictionary<SimActorId, EnemyController> _enemyControllers = new();
    readonly Dictionary<SimActorId, CharacterResourceSim> _resourcesByActor = new();
    readonly List<EnemyController> _enemyStepSnapshot = new();

    SimulationConfig _config;
    FixedStepAccumulator _accumulator;
    SimulationWorld _world;
    CombatHitPipeline _combatHits;
    ISimCollisionWorld _collisionWorld = OpenFieldSimCollisionWorld.Instance;

    /// <summary>最近完成的逻辑帧；尚未推进时为 -1。</summary>
    public long CurrentFrame => _world?.CurrentFrame ?? -1;

    /// <summary>当前固定逻辑帧秒数。</summary>
    public float FixedDeltaSeconds =>
        _config?.FixedDeltaSeconds ?? 1f / SimulationConfig.DefaultLogicHz;

    /// <summary>当前场景纯 C# 模拟世界；只读暴露给调试和后续网络宿主。</summary>
    public SimulationWorld World => _world;

    /// <summary>当前场景唯一命中收集与帧末结算流水线。</summary>
    public CombatHitPipeline CombatHits => _combatHits;

    /// <summary>场景共享静态碰撞世界；角色 MotorSim 必须使用同一实例。</summary>
    public ISimCollisionWorld CollisionWorld => _collisionWorld;

    void Awake()
    {
        _config = new SimulationConfig();
        _accumulator = new FixedStepAccumulator(
            _config.FixedDeltaSeconds,
            _config.MaxFrameCatchUp);
        _world = new SimulationWorld(_config);
        _combatHits = new CombatHitPipeline(PublishResolvedHit);
        _combatHits.BindResourceLookup(LookupResources);
    }

    /// <summary>注册 Actor 资源表，供命中 GrantOnHit。</summary>
    public void RegisterResources(SimActorId actorId, CharacterResourceSim resources)
    {
        if (!actorId.IsValid || resources == null)
            return;
        _resourcesByActor[actorId] = resources;
    }

    /// <summary>注销 Actor 资源表。</summary>
    public void UnregisterResources(SimActorId actorId)
    {
        if (actorId.IsValid)
            _resourcesByActor.Remove(actorId);
    }

    CharacterResourceSim LookupResources(SimActorId actorId)
    {
        if (!actorId.IsValid)
            return null;
        return _resourcesByActor.TryGetValue(actorId, out CharacterResourceSim sim) ? sim : null;
    }

    /// <summary>
    /// 在注册任何 Actor 之前设置静态碰撞世界；null 回退空场地。
    /// 已有 Actor 后调用会打警告且不替换（避免半场混用两套障碍）。
    /// </summary>
    public void SetCollisionWorld(ISimCollisionWorld collisionWorld)
    {
        if (_world != null && _world.ActorCount > 0)
        {
            Debug.LogWarning(
                "SimulationHost: 已有注册 Actor，忽略 SetCollisionWorld。请在角色创建前绑定烘焙资产。",
                this);
            return;
        }

        _collisionWorld = collisionWorld ?? OpenFieldSimCollisionWorld.Instance;
    }

    /// <summary>按 Input/Actor/Combat/PostCombat/Commit 单轨顺序推进本渲染帧内的全部逻辑步。</summary>
    void Update()
    {
        _world.SampleRenderFrame();
        int stepCount = _accumulator.ConsumeSteps(Time.deltaTime);
        for (int i = 0; i < stepCount; i++)
        {
            _combatHits.BeginFrame(_world.CurrentFrame + 1);
            _world.Step();
            _combatHits.ResolveBeforePostCombat(_world.CurrentFrame);
            _world.ResolvePostCombat();
            _combatHits.CompleteFrame(_world.CurrentFrame);
            CommitEnemyLifecycle();
            // 表现层按逻辑帧递减 VFX HitStop 等，禁止用 unscaled 秒倒计时
            GetArchitecture().SendEvent(SimulationLogicStepEvent.Instance);
        }
    }

    void LateUpdate()
    {
        // 模型先完成 Pose 插值，默认顺序的 CameraManager.LateUpdate 再读取同一表现帧。
        _world.Render(_accumulator.InterpolationAlpha);
    }

    void OnDestroy()
    {
        _enemyStepSnapshot.Clear();
        _enemyControllers.Clear();
        _accumulator?.Reset();
        _combatHits = null;
        _world = null;
    }

    /// <summary>把玩家 Actor 注册到固定帧 World，并返回对称注销句柄。</summary>
    public SimActorRegistration RegisterPlayer(CharacterActor actor)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));

        return _world.Register(actor);
    }

    /// <summary>把敌人句柄注册到固定帧 World，并保留 Controller 处理帧后 App 生命周期。</summary>
    public SimActorRegistration RegisterEnemy(EnemyHandle handle, EnemyController controller)
    {
        if (handle == null)
            throw new ArgumentNullException(nameof(handle));
        if (controller == null)
            throw new ArgumentNullException(nameof(controller));

        SimActorRegistration registration = _world.Register(handle);
        _enemyControllers.Add(registration.Id, controller);
        return registration;
    }

    /// <summary>注销玩家或敌人 Actor；重复注销安全返回 false。</summary>
    public bool Unregister(SimActorRegistration registration)
    {
        if (!registration.IsValid || _world == null)
            return false;

        _enemyControllers.Remove(registration.Id);
        UnregisterResources(registration.Id);
        return _world.Unregister(registration);
    }

    /// <summary>在 Combat 与 PostCombat 完成后集中执行敌人死亡注销与回收 Command。</summary>
    void CommitEnemyLifecycle()
    {
        _enemyStepSnapshot.Clear();
        foreach (EnemyController controller in _enemyControllers.Values)
            _enemyStepSnapshot.Add(controller);

        // Unity Destroy 延迟到当前 Update 末尾；快照避免回收回调改变字典枚举。
        for (int i = 0; i < _enemyStepSnapshot.Count; i++)
        {
            EnemyController controller = _enemyStepSnapshot[i];
            if (controller != null)
                controller.ProcessPostSimulationStep();
        }
    }

    /// <summary>把帧末只读命中结果发布给镜头、动画与 VFX 等表现订阅者。</summary>
    void PublishResolvedHit(ResolvedCombatHit hit)
    {
        SendCommand(new PublishAttackHitCommand(hit));
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>场景中唯一的 Unity 时间入口，把渲染帧时间转换为固定 60Hz SimulationWorld Step。</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class SimulationHost : AppControllerBase
{
    readonly Dictionary<SimActorId, EnemyController> _enemyControllers = new();
    readonly Dictionary<SimActorId, NumericSystem> _numericByActor = new();
    readonly List<EnemyController> _enemyStepSnapshot = new();
    readonly List<ReplicatedHitEvent> _frameHits = new();

    SimulationConfig _config;
    SimulationStepKernel _kernel;
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

    float _externalInterpolationAlpha;

    /// <summary>当前渲染帧相对上一逻辑步的插值比例，供幽灵与权威表现共用。</summary>
    public float InterpolationAlpha => DriveFromExternalClock
        ? _externalInterpolationAlpha
        : _kernel != null ? _kernel.InterpolationAlpha : 0f;

    /// <summary>为 true 时 Update/LateUpdate 不自动步进；由 ServerSimulationRunner 调 StepOnce。</summary>
    public bool DriveFromExternalClock { get; set; }

    /// <summary>外部时钟步进后写入插值比例；Listen 本机 Render 依赖此值。</summary>
    public void PublishExternalInterpolationAlpha(float alpha) =>
        _externalInterpolationAlpha = alpha;

    /// <summary>每个逻辑步在 Combat/PostCombat/生命周期提交之后触发；参数为权威帧号。</summary>
    public event Action<long> AfterLogicStep;

    /// <summary>本逻辑步已发布的权威命中（AfterLogicStep 内可读，步末清空）。</summary>
    public IReadOnlyList<ReplicatedHitEvent> FrameHits => _frameHits;

    void Awake()
    {
        _config = new SimulationConfig();
        _kernel = new SimulationStepKernel(_config);
        _world = new SimulationWorld(_config);
        _combatHits = new CombatHitPipeline(PublishResolvedHit);
        _combatHits.BindNumericLookup(LookupNumeric);
    }

    /// <summary>注册 Actor Numeric，供命中 Grant / 完美闪避武装。</summary>
    public void RegisterNumeric(SimActorId actorId, NumericSystem numeric)
    {
        if (!actorId.IsValid || numeric == null)
            return;
        _numericByActor[actorId] = numeric;
    }

    /// <summary>注销 Actor Numeric。</summary>
    public void UnregisterNumeric(SimActorId actorId)
    {
        if (actorId.IsValid)
            _numericByActor.Remove(actorId);
    }

    /// <summary>供 Hurtbox 查攻击者 Numeric；未注册返回 null。</summary>
    public NumericSystem LookupNumeric(SimActorId actorId)
    {
        if (!actorId.IsValid)
            return null;
        return _numericByActor.TryGetValue(actorId, out NumericSystem numeric) ? numeric : null;
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

    /// <summary>汇聚本渲染帧设备边沿；Dedicated 外部时钟每拍调用一次。</summary>
    public void SampleRenderInputs() => _world?.SampleRenderFrame();

    /// <summary>按 Input/Actor/Combat/PostCombat/Commit 推进恰好一个逻辑步。</summary>
    public void StepOnce()
    {
        if (_world == null || _combatHits == null)
            return;

        _combatHits.BeginFrame(_world.CurrentFrame + 1);
        _world.Step();
        _combatHits.ResolveBeforePostCombat(_world.CurrentFrame);
        _world.ResolvePostCombat();
        _combatHits.CompleteFrame(_world.CurrentFrame);
        CommitEnemyLifecycle();
        GetArchitecture().SendEvent(SimulationLogicStepEvent.Instance);
        AfterLogicStep?.Invoke(_world.CurrentFrame);
        _frameHits.Clear();
    }

    /// <summary>按 Input/Actor/Combat/PostCombat/Commit 单轨顺序推进本渲染帧内的全部逻辑步。</summary>
    void Update()
    {
        if (DriveFromExternalClock)
            return;

        SampleRenderInputs();
        int stepCount = _kernel.ConsumeSteps(Time.deltaTime, out _);
        for (int i = 0; i < stepCount; i++)
            StepOnce();
    }

    void LateUpdate()
    {
        if (DriveFromExternalClock)
            return;

        // 模型先完成 Pose 插值，默认顺序的 CameraManager.LateUpdate 再读取同一表现帧。
        _world.Render(_kernel.InterpolationAlpha);
    }

    void OnDestroy()
    {
        _enemyStepSnapshot.Clear();
        _enemyControllers.Clear();
        _kernel?.Reset();
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

    /// <summary>把当前仍注册的敌人控制器拷入列表，供复制打包；不包含本机玩家。</summary>
    public void CopyEnemyControllers(List<EnemyController> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        results.Clear();
        foreach (EnemyController controller in _enemyControllers.Values)
        {
            if (controller != null)
                results.Add(controller);
        }
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
        UnregisterNumeric(registration.Id);
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
        if (!hit.AbsorbedByPerfectDodge
            && hit.Key.AttackerId.IsValid
            && hit.Key.TargetId.IsValid)
        {
            _frameHits.Add(new ReplicatedHitEvent(
                _world.CurrentFrame,
                hit.Key,
                actionId: 0,
                hit.ReactionKind,
                MotionQuantization.MetersToMm(hit.HitPoint.x),
                MotionQuantization.MetersToMm(hit.HitPoint.y),
                MotionQuantization.MetersToMm(hit.HitPoint.z),
                MotionQuantization.MetersToMm(hit.HitDirection.x),
                MotionQuantization.MetersToMm(hit.HitDirection.z)));
        }

        SendCommand(new PublishAttackHitCommand(hit));
    }
}

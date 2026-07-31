using UnityEngine;

/// <summary>单敌人 Scene/App 入口；按固定顺序推进 Brain 与共享 CharacterActor。</summary>
[DisallowMultipleComponent]
public sealed class EnemyController : AppControllerBase
{
    [SerializeField] EnemyDefinition enemyDefinition = null;
    [SerializeField] Transform target = null;

    EnemyHandle _handle;
    bool _registered;
    bool _despawnRequested;
    SimulationHost _simulationHost;
    SimActorRegistration _simulationRegistration;

    /// <summary>当前敌人定义。</summary>
    public EnemyDefinition Definition => enemyDefinition;
    /// <summary>当前生命值；尚未装配时为 0。</summary>
    public float CurrentHealth => _handle != null ? _handle.CurrentHealth : 0f;
    /// <summary>当前 AI 状态。</summary>
    public EnemyBrainState BrainState =>
        _handle != null ? _handle.BrainState : EnemyBrainState.Idle;
    /// <summary>敌人是否已经死亡。</summary>
    public bool IsDead => _handle?.IsDead == true;

    /// <summary>供 SpawnEnemyCommand 在 Start 前注入定义、位置与目标。</summary>
    public void Initialize(EnemyDefinition definition, Transform targetTransform)
    {
        enemyDefinition = definition;
        target = targetTransform;
    }

    void Start()
    {
        if (!TryBuild())
        {
            GetSystem<EnemySpawnSystem>()?.Unregister(this);
            enabled = false;
        }
    }

    void OnEnable()
    {
        _handle?.Enable();
        RegisterSimulationActor();
    }

    void OnDisable()
    {
        UnregisterSimulationActor();
        _handle?.Disable();
    }

    void OnDestroy()
    {
        UnregisterSimulationActor();
        UnregisterCombatEntries();
        GetSystem<EnemySpawnSystem>()?.Unregister(this);
        _handle?.Dispose();
        _handle = null;
        _simulationHost = null;
        _simulationRegistration = SimActorRegistration.Invalid;
    }

    /// <summary>由 DespawnEnemyCommand 调用，统一走 Unity 生命周期完成释放。</summary>
    public void Despawn()
    {
        if (this != null)
            Destroy(gameObject);
    }

    /// <summary>校验配置并创建纯 C# 敌人服务图。</summary>
    bool TryBuild()
    {
        if (enemyDefinition == null)
        {
            Debug.LogError("EnemyController: 未绑定 EnemyDefinition。", this);
            return false;
        }

        if (!enemyDefinition.Validate(this))
            return false;

        if (target == null)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            target = player != null ? player.transform : null;
        }

        CombatWorldController combatWorld = EnsureCombatWorldController();
        _handle = EnemyActorFactory.Create(
            gameObject,
            transform,
            enemyDefinition,
            () => target,
            () => SendQuery(new GetActiveTargetsQuery()),
            ApplyDetectedHit,
            new CharacterReactionResolver(enemyDefinition.CharacterConfig.Combat.Reactions));

        GetSystem<CombatActorSystem>()?.Register(
            transform,
            _handle.Actor,
            _handle.ActionExecutor,
            _handle.Animation);
        GetSystem<TargetSystem>()?.Register(_handle.Target);
        GetSystem<EnemySpawnSystem>()?.Register(this);
        _registered = true;
        _simulationHost = combatWorld.EnsureSimulationHost();
        RegisterSimulationActor();
        _handle.Enable();
        gameObject.name = enemyDefinition.DisplayName;
        return true;
    }

    /// <summary>由 SimulationHost 在每个逻辑帧后处理死亡注销与回收副作用。</summary>
    internal void ProcessPostSimulationStep()
    {
        if (_handle == null)
            return;

        if (_handle.IsDead)
            UnregisterCombatEntries();

        if (!_handle.IsReadyToDespawn || _despawnRequested)
            return;

        _despawnRequested = true;
        SendCommand(new DespawnEnemyCommand(this));
    }

    /// <summary>启用后把已装配敌人注册到唯一固定帧 World。</summary>
    void RegisterSimulationActor()
    {
        if (_handle == null || _simulationHost == null || _simulationRegistration.IsValid)
            return;

        _simulationRegistration = _simulationHost.RegisterEnemy(_handle, this);
    }

    /// <summary>禁用或销毁时从固定帧 World 对称注销，避免停用对象继续推进。</summary>
    void UnregisterSimulationActor()
    {
        if (_simulationHost != null)
            _simulationHost.Unregister(_simulationRegistration);

        _simulationRegistration = SimActorRegistration.Invalid;
    }

    /// <summary>把纯 Domain 命中检测结果交给统一 ApplyHitCommand。</summary>
    void ApplyDetectedHit(
        ActionHitContext context,
        IHurtboxTarget hitTarget,
        IActionHitReceiver hitReceiver,
        Transform targetTransform)
    {
        SendCommand(new ApplyHitCommand(context, hitTarget, hitReceiver, targetTransform));
    }

    /// <summary>死亡时立即注销战斗与索敌条目，避免回收延迟期间仍被选中。</summary>
    void UnregisterCombatEntries()
    {
        if (!_registered)
            return;

        GetSystem<TargetSystem>()?.Unregister(_handle?.Target);
        GetSystem<CombatActorSystem>()?.Unregister(transform);
        _registered = false;
    }

    /// <summary>确保敌人独立运行时也存在统一战斗世界锚点并返回该入口。</summary>
    CombatWorldController EnsureCombatWorldController()
    {
        CombatWorldController world = CombatWorldController.Current;
        if (world == null)
            world = FindObjectOfType<CombatWorldController>();
        if (world != null)
            return world;

        var worldObject = new GameObject("CombatWorldController");
        return worldObject.AddComponent<CombatWorldController>();
    }
}

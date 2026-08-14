using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>单敌人 Scene/App 入口；按固定顺序推进 Brain 与共享 CharacterActor。</summary>
[DisallowMultipleComponent]
public sealed class EnemyController : AppControllerBase
{
    [SerializeField] EnemyDefinition enemyDefinition = null;
    [SerializeField] Transform target = null;
    [SerializeField] bool drawBehaviorDebugGizmos = true;
    [SerializeField] bool logBehaviorTreeEachStep = false;

    EnemyHandle _handle;
    Transform[] _pinnedPlayerRoots;
    bool _registered;
    bool _despawnRequested;
    SimulationHost _simulationHost;
    SimActorRegistration _simulationRegistration;
    string _lastLoggedDebugKey;

    /// <summary>当前敌人定义。</summary>
    public EnemyDefinition Definition => enemyDefinition;

    /// <summary>已装配的权威角色；未创建时为 null。</summary>
    public CharacterActor Actor => _handle?.Actor;

    /// <summary>复制幽灵用的角色配置；未绑定时为 null。</summary>
    public CharacterConfig CharacterConfig => enemyDefinition != null
        ? enemyDefinition.CharacterConfig
        : null;
    /// <summary>当前生命值；尚未装配时为 0。</summary>
    public float CurrentHealth => _handle != null ? _handle.CurrentHealth : 0f;
    /// <summary>当前 AI 状态。</summary>
    public EnemyBrainState BrainState =>
        _handle != null ? _handle.BrainState : EnemyBrainState.Idle;
    /// <summary>敌人是否已经死亡。</summary>
    public bool IsDead => _handle?.IsDead == true;

    /// <summary>行为树调试路径（Graph 编辑器 Play 高亮用）。</summary>
    public string DebugBehaviorPath =>
        _handle != null ? _handle.BrainLastDebugPath : string.Empty;

    /// <summary>打开 Brain 路径采集（供 Graph 调试高亮）。</summary>
    public void EnsureBehaviorDebugEnabled() => _handle?.SetBrainDebugEnabled(true);

    /// <summary>供 SpawnEnemyCommand 在 Start 前注入定义；targetTransform 非空时钉死单目标，否则读玩家花名册。</summary>
    public void Initialize(EnemyDefinition definition, Transform targetTransform)
    {
        enemyDefinition = definition;
        target = targetTransform;
    }

    /// <summary>
    /// 感知候选：Inspector/命令钉死的单目标优先，否则查询全部玩家根。
    /// 不得 FindObjectOfType 唯一玩家。
    /// </summary>
    IReadOnlyList<Transform> ResolvePlayerRoots()
    {
        if (target != null)
        {
            if (_pinnedPlayerRoots == null || _pinnedPlayerRoots[0] != target)
                _pinnedPlayerRoots = new[] { target };
            return _pinnedPlayerRoots;
        }

        IReadOnlyList<Transform> roots = SendQuery(new GetPlayerRootsQuery());
        return roots ?? Array.Empty<Transform>();
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

        CombatWorldController combatWorld = EnsureCombatWorldController();
        _simulationHost = combatWorld.EnsureSimulationHost();
        _handle = EnemyActorFactory.Create(
            gameObject,
            transform,
            enemyDefinition,
            ResolvePlayerRoots,
            () => SendQuery(new GetActiveTargetsQuery()),
            _simulationHost.CombatHits,
            new CharacterReactionResolver(enemyDefinition.CharacterConfig.Combat.Reactions),
            _simulationHost.CollisionWorld);

        GetSystem<CombatActorSystem>()?.Register(
            transform,
            _handle.Actor,
            _handle.Animation);
        GetSystem<TargetSystem>()?.Register(_handle.Target);
        GetSystem<EnemySpawnSystem>()?.Register(this);
        _registered = true;
        RegisterSimulationActor();
        _handle.Enable();
        _handle.SetBrainDebugEnabled(drawBehaviorDebugGizmos || logBehaviorTreeEachStep);
        gameObject.name = enemyDefinition.DisplayName;
        return true;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawBehaviorDebugGizmos || _handle == null)
            return;

#if UNITY_EDITOR
        string path = _handle.BrainLastDebugPath;
        if (string.IsNullOrEmpty(path))
            path = "(no path)";
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2.2f,
            $"{BrainState}\n{_handle.BrainLastRunnerStatus}\n{path}");
#endif
    }

    /// <summary>由 SimulationHost 在每个逻辑帧后处理死亡注销与回收副作用。</summary>
    internal void ProcessPostSimulationStep()
    {
        if (_handle == null)
            return;

        if (logBehaviorTreeEachStep)
            TryLogBehaviorDebug();

        if (_handle.IsDead)
            UnregisterCombatEntries();

        if (!_handle.IsReadyToDespawn || _despawnRequested)
            return;

        _despawnRequested = true;
        SendCommand(new DespawnEnemyCommand(this));
    }

    /// <summary>状态或路径变化时打一条 BT 调试日志，避免每帧刷屏。</summary>
    void TryLogBehaviorDebug()
    {
        string key = $"{BrainState}|{_handle.BrainLastRunnerStatus}|{_handle.BrainLastDebugPath}";
        if (key == _lastLoggedDebugKey)
            return;
        _lastLoggedDebugKey = key;
        Debug.Log($"[EnemyBT] {name} {key}", this);
    }

    /// <summary>启用后把已装配敌人注册到唯一固定帧 World。</summary>
    void RegisterSimulationActor()
    {
        if (_handle == null || _simulationHost == null || _simulationRegistration.IsValid)
            return;

        _simulationRegistration = _simulationHost.RegisterEnemy(_handle, this);
        if (_handle?.Actor != null)
            _simulationHost.RegisterNumeric(_handle.Actor.SimulationId, _handle.Actor.Numeric);
        _handle?.Target?.SetNumericLookup(_simulationHost.LookupNumeric);
    }

    /// <summary>禁用或销毁时从固定帧 World 对称注销，避免停用对象继续推进。</summary>
    void UnregisterSimulationActor()
    {
        if (_simulationHost != null)
            _simulationHost.Unregister(_simulationRegistration);

        _simulationRegistration = SimActorRegistration.Invalid;
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

using UnityEngine;

/// <summary>单敌人纯 C# 生命周期句柄；聚合 Brain、角色 Actor、生命值与受击目标。</summary>
public sealed class EnemyHandle : System.IDisposable
{
    readonly EnemyDefinition _definition;
    readonly CharacterActor _actor;
    readonly EnemyBrain _brain;
    readonly EnemyHealth _health;
    readonly Transform _facingProxy;
    readonly CharacterReactionService _reactionService;
    float _deathReadyElapsed;

    /// <summary>创建已装配的敌人句柄并接管反应服务生命周期。</summary>
    public EnemyHandle(
        EnemyDefinition definition,
        Transform root,
        CharacterActor actor,
        ActionExecutor actionExecutor,
        CharacterAnimationService animation,
        EnemyBrain brain,
        EnemyHealth health,
        CharacterHurtboxTarget target,
        Transform facingProxy,
        CharacterReactionService reactionService)
    {
        _definition = definition;
        Root = root;
        _actor = actor;
        ActionExecutor = actionExecutor;
        Animation = animation;
        _brain = brain;
        _health = health;
        Target = target;
        _facingProxy = facingProxy;
        _reactionService = reactionService;
    }

    /// <summary>敌人根节点。</summary>
    public Transform Root { get; }
    /// <summary>用于 CombatActorSystem 注册的共享角色实例。</summary>
    public CharacterActor Actor => _actor;
    /// <summary>用于架构注册的动作执行器。</summary>
    public ActionExecutor ActionExecutor { get; }
    /// <summary>用于架构注册与卡肉的动画门面。</summary>
    public CharacterAnimationService Animation { get; }
    /// <summary>可命中、可索敌目标。</summary>
    public CharacterHurtboxTarget Target { get; }
    /// <summary>敌人定义。</summary>
    public EnemyDefinition Definition => _definition;
    /// <summary>当前 AI 状态。</summary>
    public EnemyBrainState BrainState => _brain.State;
    /// <summary>当前生命值。</summary>
    public float CurrentHealth => _health.CurrentHealth;
    /// <summary>生命值是否归零。</summary>
    public bool IsDead => _health.IsDead;
    /// <summary>死亡表现与额外等待均完成，可执行回收。</summary>
    public bool IsReadyToDespawn =>
        IsDead
        && _actor.DeathPresentationComplete
        && _deathReadyElapsed >= _definition.BrainProfile.DeathDespawnDelaySeconds;

    /// <summary>启用 AI 输入源。</summary>
    public void Enable() => _actor.Enable();

    /// <summary>禁用 AI 输入源。</summary>
    public void Disable() => _actor.Disable();

    /// <summary>按 Brain 决策在前、角色管线在后的固定顺序推进。</summary>
    public void Tick(float deltaTime)
    {
        if (!IsDead)
            _brain.Tick(deltaTime);

        _actor.Tick(deltaTime);
        if (IsDead && _actor.DeathPresentationComplete)
            _deathReadyElapsed += Mathf.Max(0f, deltaTime);
    }

    /// <summary>停止决策、解绑事件并释放角色运行时资源。</summary>
    public void Dispose()
    {
        _reactionService?.Dispose();
        _brain.Stop();
        _actor.Disable();
        _actor.Dispose();

        if (_facingProxy != null)
            UnityEngine.Object.Destroy(_facingProxy.gameObject);
    }

}

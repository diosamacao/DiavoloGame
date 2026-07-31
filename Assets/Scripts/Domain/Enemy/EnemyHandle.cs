using UnityEngine;

/// <summary>单敌人纯 C# 生命周期句柄；在 World 输入阶段先决策，再消费同帧量化输入。</summary>
public sealed class EnemyHandle :
    System.IDisposable,
    ISimulationActor,
    ISimulationInputParticipant,
    ISimulationInputProducer,
    ISimulationRenderable
{
    readonly EnemyDefinition _definition;
    readonly CharacterActor _actor;
    readonly EnemyBrain _brain;
    readonly AIInputWriter _input;
    readonly EnemyHealth _health;
    readonly Transform _facingProxy;
    readonly CharacterReactionService _reactionService;
    InputFrameBuffer _inputFrames;
    SimActorId _actorId;
    float _deathReadyElapsed;

    /// <summary>创建已装配的敌人句柄并接管反应服务生命周期。</summary>
    public EnemyHandle(
        EnemyDefinition definition,
        Transform root,
        CharacterActor actor,
        ActionExecutor actionExecutor,
        CharacterAnimationService animation,
        EnemyBrain brain,
        AIInputWriter input,
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
        _input = input;
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

    /// <summary>启用 AI 输入帧写入。</summary>
    public void Enable() => _input.Enable();

    /// <summary>禁用并清空 AI 输入帧写入。</summary>
    public void Disable() => _input.Disable();

    /// <summary>注册时绑定句柄与内部 CharacterActor 到同一稳定输入身份。</summary>
    public void BindSimulationInput(SimActorId actorId, InputFrameBuffer inputFrames)
    {
        _actorId = actorId;
        _inputFrames = inputFrames ?? throw new System.ArgumentNullException(nameof(inputFrames));
        _actor.BindSimulationInput(actorId, inputFrames);
    }

    /// <summary>在所有 Actor Step 前基于上一帧状态决策并写入当前逻辑帧输入。</summary>
    public void ProduceInput(long frameIndex)
    {
        if (!IsDead)
            _brain.Step();

        InputFrame input = _input.BuildFrame(frameIndex, _actorId);
        _inputFrames.Set(in input);
    }

    /// <summary>消费 World 已准备的同帧输入并推进共享角色管线。</summary>
    public void Step(long frameIndex, float fixedDeltaSeconds, in InputFrame inputFrame)
    {
        _actor.Step(frameIndex, fixedDeltaSeconds, in inputFrame);
        if (IsDead && _actor.DeathPresentationComplete)
            _deathReadyElapsed += Mathf.Max(0f, fixedDeltaSeconds);
    }

    /// <summary>把内部角色的前后逻辑 Pose 插值到敌人模型表现锚点。</summary>
    public void Render(float interpolationAlpha) => _actor.Render(interpolationAlpha);

    /// <summary>停止决策、解绑事件并释放角色运行时资源。</summary>
    public void Dispose()
    {
        _reactionService?.Dispose();
        _brain.Stop();
        _input.Disable();
        _actor.Dispose();

        if (_facingProxy != null)
            UnityEngine.Object.Destroy(_facingProxy.gameObject);
    }

}

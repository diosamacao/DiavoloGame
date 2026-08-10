using UnityEngine;

/// <summary>单敌人纯 C# 生命周期句柄；在 World 输入阶段先决策，再消费同帧量化输入。</summary>
public sealed class EnemyHandle :
    System.IDisposable,
    ISimulationActor,
    ISimulationInputParticipant,
    ISimulationInputProducer,
    ISimulationRenderable,
    ISimulationPostCombatActor,
    ISimSoftBodyParticipant
{
    readonly EnemyDefinition _definition;
    readonly CharacterActor _actor;
    readonly EnemyBrain _brain;
    readonly AIInputWriter _input;
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
        ActionSim actionSim,
        CharacterAnimationService animation,
        EnemyBrain brain,
        AIInputWriter input,
        CharacterHurtboxTarget target,
        Transform facingProxy,
        CharacterReactionService reactionService)
    {
        _definition = definition;
        Root = root;
        _actor = actor;
        ActionSim = actionSim;
        Animation = animation;
        _brain = brain;
        _input = input;
        Target = target;
        _facingProxy = facingProxy;
        _reactionService = reactionService;
    }

    public Transform Root { get; }
    public CharacterActor Actor => _actor;
    public ActionSim ActionSim { get; }
    public CharacterAnimationService Animation { get; }
    public CharacterHurtboxTarget Target { get; }
    public EnemyDefinition Definition => _definition;
    public EnemyBrainState BrainState => _brain.State;
    /// <summary>BT 调试：上一帧 Runner 状态。</summary>
    public BehaviorStatus BrainLastRunnerStatus => _brain.LastRunnerStatus;
    /// <summary>BT 调试：NamedNode 路径。</summary>
    public string BrainLastDebugPath => _brain.LastDebugPath;
    /// <summary>BT 调试：上一帧 CombatRequest Entry。</summary>
    public string BrainLastCombatRequestEntryId => _brain.DebugCombatRequestEntryId;
    /// <summary>开关行为树调试采集。</summary>
    public void SetBrainDebugEnabled(bool enabled) => _brain.SetDebugEnabled(enabled);
    public float CurrentHealth => _actor.Vitality.CurrentHealth;
    public bool IsDead => _actor.Vitality.IsDead;
    public bool IsReadyToDespawn =>
        IsDead
        && _actor.DeathPresentationComplete
        && _deathReadyElapsed >= _definition.BrainProfile.DeathDespawnDelaySeconds;

    public void Enable() => _input.Enable();
    public void Disable() => _input.Disable();

    public void BindSimulationInput(SimActorId actorId, InputFrameBuffer inputFrames)
    {
        _actorId = actorId;
        _inputFrames = inputFrames ?? throw new System.ArgumentNullException(nameof(inputFrames));
        _actor.BindSimulationInput(actorId, inputFrames);
    }

    public void ProduceInput(long frameIndex)
    {
        if (!IsDead)
            _brain.Step();

        InputFrame input = _input.BuildFrame(frameIndex, _actorId);
        _inputFrames.Set(in input);
    }

    public void Step(long frameIndex, float fixedDeltaSeconds, in InputFrame inputFrame)
    {
        _actor.Step(frameIndex, fixedDeltaSeconds, in inputFrame);
        if (IsDead && _actor.DeathPresentationComplete)
            _deathReadyElapsed += Mathf.Max(0f, fixedDeltaSeconds);
    }

    public void ResolvePostCombat(long frameIndex) => _actor.ResolvePostCombat(frameIndex);

    public CharacterMotorSim MotorSim => _actor.MotorSim;

    public bool ParticipatesInSoftBodySeparation =>
        !IsDead && _actor.ParticipatesInSoftBodySeparation;

    public void OnSoftBodySeparationApplied() => _actor.OnSoftBodySeparationApplied();

    public void Render(float interpolationAlpha) => _actor.Render(interpolationAlpha);

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

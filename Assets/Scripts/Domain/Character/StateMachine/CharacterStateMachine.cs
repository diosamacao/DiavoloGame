/// <summary>角色状态机；不挂载到 GameObject，由 CharacterActor 持有并 Tick。</summary>
public sealed class CharacterStateMachine : ICharacterStateMachine
{
    readonly StateMachine<CharacterStateType, CharacterContext> _machine = new();
    readonly ActionState _actionState = new();
    readonly HitState _hitState = new();
    readonly DeathState _deathState = new();

    CharacterContext Context => _machine.Context;

    public CharacterStateType CurrentStateId => _machine.CurrentStateId;

    /// <summary>顶层 Locomotion 时的内层步态；供复制 Gait 字段。非 Locomotion 为 Walk。</summary>
    public LocomotionGait ReplicationGait
    {
        get
        {
            if (CurrentStateId != CharacterStateType.Locomotion)
                return LocomotionGait.Walk;
            LocomotionStateMachine locomotion = Context.LocomotionStateMachine;
            return locomotion != null ? locomotion.Gait : LocomotionGait.Walk;
        }
    }

    /// <summary>
    /// L-DIR4：仅顶层 Locomotion 时取内层 Sprint 倾身 Roll；其它状态为 0。
    /// </summary>
    public float SprintLeanRollDegrees
    {
        get
        {
            if (CurrentStateId != CharacterStateType.Locomotion)
                return 0f;
            LocomotionStateMachine locomotion = Context.LocomotionStateMachine;
            return locomotion != null ? locomotion.Context.SprintLeanRollDegrees : 0f;
        }
    }

    /// <summary>L-DIR3：Locomotion 有效朝向为 FaceTarget（Profile 声明且有 SelectedTarget）；供相机关闭跟朝向。</summary>
    public bool IsLocomotionFaceTargetActive
    {
        get
        {
            if (CurrentStateId != CharacterStateType.Locomotion)
                return false;
            LocomotionStateMachine locomotion = Context.LocomotionStateMachine;
            return locomotion != null
                && locomotion.Context.ResolveFacingMode() == LocomotionFacingMode.FaceTarget;
        }
    }

    /// <summary>创建角色状态机并注册 Locomotion、Action、Hit 与 Death 状态。</summary>
    public CharacterStateMachine(CharacterContext context)
    {
        context.StateMachine = this;
        RegisterStates();
        _machine.Initialize(context, CharacterStateType.Locomotion);
    }

    void RegisterStates()
    {
        RegisterState(new LocomotionState());
        RegisterState(_actionState);
        RegisterState(_hitState);
        RegisterState(_deathState);
    }

    void RegisterState(CharacterState state) => _machine.RegisterState(state);

    /// <summary>推进当前状态。</summary>
    public void Tick(float deltaTime)
    {
        _machine.Tick(deltaTime);
    }

    /// <summary>在整帧 Combat Resolve 后让当前状态按逻辑动作会话结果完成收尾。</summary>
    public void ResolvePostCombat()
    {
        switch (CurrentStateId)
        {
            case CharacterStateType.Action:
                _actionState.ResolvePostCombat();
                break;
            case CharacterStateType.Hit:
                _hitState.ResolvePostCombat();
                break;
            case CharacterStateType.Death:
                _deathState.ResolvePostCombat();
                break;
        }
    }

    public bool TryChangeState(CharacterStateType next, bool force = false) =>
        _machine.TryChangeState(next, force);

    /// <summary>死亡表现是否已经播放完成。</summary>
    public bool DeathPresentationComplete => Context.DeathPresentationComplete;

    /// <summary>强制进入或重入受击状态，并覆盖上一条反应请求。</summary>
    public void EnterHit(in CharacterReactionRequest request)
    {
        Context.SetReactionRequest(in request);
        _machine.TryChangeState(CharacterStateType.Hit, force: true);
    }

    /// <summary>写入死亡表现并强制进入不可逆 Death 状态。</summary>
    public void EnterDeath(in CharacterReactionRequest request)
    {
        if (CurrentStateId == CharacterStateType.Death)
            return;

        Context.SetReactionRequest(in request);
        _machine.TryChangeState(CharacterStateType.Death, force: true);
    }

    /// <summary>由 Motor 层每帧 Push Locomotion 快照；在 Tick 之前调用，替代子类拉取 PlayerController。</summary>
    public void PushMotorSnapshot(float moveInputMagnitude, float runThreshold, bool isGrounded)
    {
        Context.MoveInputMagnitude = moveInputMagnitude;
        Context.RunThreshold = runThreshold;
        Context.IsGrounded = isGrounded;
    }
}

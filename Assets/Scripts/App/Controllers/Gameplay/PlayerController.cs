using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>玩家座位：Listen/Client 装配 Autonomous Actor（不进 World）；Dedicated 禁用本机座位。</summary>
[DefaultExecutionOrder(-50)]
public class PlayerController : AppControllerBase, ILocalPlayer
{
    [Header("References")]
    [SerializeField] PartyLoadout partyLoadout = null;

    [Header("Debug")]
    [Tooltip("Play 时在脚底画 wish（黄）与模型朝向（品红）实心箭头。")]
    [SerializeField] bool drawFacingDebugArrows = true;

    CharacterActor[] _partyActors = Array.Empty<CharacterActor>();
    GameObject[] _partyRoots = Array.Empty<GameObject>();
    PartyCombatCoordinator _partyCoordinator;
    SimulationHost simulationHost;
    CharacterFacingDebugVisualizer _facingDebugVisualizer;
    ILocalInputSampler _inputSampler;
    bool _clientSeat;
    long _predictedSwitchFrame = -1;

    /// <summary>当前 Active 角色 Actor；供预测、相机、HUD 与 Scene Gizmo 只读访问。</summary>
    public CharacterActor Actor =>
        _partyCoordinator != null
        && _partyCoordinator.ActiveIndex >= 0
        && _partyCoordinator.ActiveIndex < _partyActors.Length
            ? _partyActors[_partyCoordinator.ActiveIndex]
            : null;

    /// <summary>按 Loadout 槽位对齐的本机 Actor；空槽对应 null。</summary>
    public IReadOnlyList<CharacterActor> PartyActors => _partyActors;

    /// <summary>当前预测或权威 Active 槽索引；尚未初始化时为 -1。</summary>
    public int ActivePartySlot => _partyCoordinator?.ActiveIndex ?? -1;

    /// <summary>本座位声明的 1～3 人出战阵容。</summary>
    public PartyLoadout PartyLoadout => partyLoadout;

    /// <summary>
    /// 当前 Active 角色配置；初始化协调器前回退到开局槽。
    /// </summary>
    public CharacterConfig CharacterConfig =>
        ActiveCharacterDefinition?.CharacterConfig;

    /// <summary>当前 Active 角色定义；初始化前返回 Loadout 开局角色。</summary>
    public CharacterDefinition ActiveCharacterDefinition
    {
        get
        {
            if (partyLoadout == null)
                return null;
            int index = _partyCoordinator != null
                ? _partyCoordinator.ActiveIndex
                : partyLoadout.StartingSlot;
            return index >= 0 && index < partyLoadout.Members.Count
                ? partyLoadout.Members[index]
                : null;
        }
    }

    /// <summary>量化输入中枢；Autonomous 座位都有 Actor。</summary>
    public InputManager Input => Actor?.Input;

    /// <inheritdoc />
    public bool HasMoveIntent =>
        Actor?.Input != null
            ? Actor.Input.HasMoveIntent
            : _inputSampler != null && _inputSampler.HasMoveIntent;

    /// <inheritdoc />
    public bool IsPresentingAction =>
        Actor != null && Actor.CurrentState != CharacterStateType.Locomotion;

    /// <summary>本机设备采样；上行命令读它，不写权威 World。</summary>
    public ILocalInputSampler InputSampler => _inputSampler;

    /// <summary>相机表现使用的本地渲染帧视角输入，不进入锁步输入帧。</summary>
    public Vector2 LookInput => _inputSampler != null
        ? _inputSampler.LookInput
        : Actor?.LookInput ?? Vector2.zero;

    /// <summary>相机相对移动轴；跟朝向只认本机设备采样，不读权威 InputFrame。</summary>
    public Vector2 MoveInput => _inputSampler != null
        ? _inputSampler.MoveInput
        : Actor?.Input != null ? Actor.Input.MoveIntent : Vector2.zero;

    /// <summary>本渲染帧是否按下纯表现 CameraLock。</summary>
    public bool CameraLockPressedThisFrame => _inputSampler != null
        ? _inputSampler.CameraLockPressedThisFrame
        : Actor != null && Actor.CameraLockPressedThisFrame;

    /// <summary>玩家当前生命值；运行时未创建时为 0。</summary>
    public float CurrentHealth => Actor != null ? Actor.Vitality.CurrentHealth : 0f;

    /// <summary>相机应跟随的插值表现锚点；两端都跟 Actor。</summary>
    public Transform PresentationRoot =>
        Actor?.PresentationRoot != null ? Actor.PresentationRoot : transform;

    /// <summary>表现根；敌人感知应走权威 RemotePlayerSeat，不读本机预测根。</summary>
    public Transform Root =>
        _partyCoordinator != null
        && _partyCoordinator.ActiveIndex >= 0
        && _partyCoordinator.ActiveIndex < _partyRoots.Length
        && _partyRoots[_partyCoordinator.ActiveIndex] != null
            ? _partyRoots[_partyCoordinator.ActiveIndex].transform
            : transform;

    /// <summary>Listen / Client 本机座位恒为 true；Dedicated 不装配本机玩家。</summary>
    public bool IsLocalPredicted => _clientSeat;

    void Awake()
    {
        if (partyLoadout == null)
        {
            Debug.LogError("PlayerController: 未绑定 PartyLoadout。", this);
            enabled = false;
            return;
        }

        if (!partyLoadout.Validate(this))
        {
            enabled = false;
            return;
        }

        if (!ValidatePartyForPlayer())
        {
            enabled = false;
            return;
        }

        InputActionAsset inputActions = GameInputSettings.Active;
        if (inputActions == null)
        {
            Debug.LogError("PlayerController: 全局 InputActionAsset 未就绪（GameInputSettings）。", this);
            enabled = false;
            return;
        }

        CombatWorldController combatWorld = EnsureCombatWorldController();
        // Dedicated 进程禁止装配本机玩家座位；权威角色只由远端 Join 创建。
        if (combatWorld != null && combatWorld.Role == ReplicationRole.DedicatedServer)
        {
            enabled = false;
            return;
        }

        BuildClientSeat(inputActions);
    }

    /// <summary>装配前校验阵容中每个非空角色，避免后台槽直到切出时才暴露缺失配置。</summary>
    bool ValidatePartyForPlayer()
    {
        bool valid = true;
        IReadOnlyList<CharacterDefinition> members = partyLoadout.Members;
        for (int i = 0; i < members.Count; i++)
        {
            CharacterConfig config = members[i]?.CharacterConfig;
            if (config != null && !config.ValidateForPlayer(this))
                valid = false;
        }

        return valid;
    }

    void OnEnable()
    {
        if (!_clientSeat)
            return;

        _inputSampler?.Enable();
        GetSystem<LocalPlayerService>()?.Register(this, isLocalOwner: true);
        EnsureFacingDebugVisualizer();
    }

    void LateUpdate()
    {
        if (_facingDebugVisualizer != null)
            _facingDebugVisualizer.SetDrawEnabled(drawFacingDebugArrows);
    }

    /// <summary>开发构建下挂载脚底朝向调试箭头（wish / 模型）。</summary>
    void EnsureFacingDebugVisualizer()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_facingDebugVisualizer == null)
            _facingDebugVisualizer = GetComponent<CharacterFacingDebugVisualizer>();
        if (_facingDebugVisualizer == null)
            _facingDebugVisualizer = gameObject.AddComponent<CharacterFacingDebugVisualizer>();
        _facingDebugVisualizer.Bind(Actor);
        _facingDebugVisualizer.SetDrawEnabled(drawFacingDebugArrows);
#endif
    }

    void OnDisable()
    {
        GetSystem<LocalPlayerService>()?.Unregister(this);
        if (!_clientSeat)
            return;

        _inputSampler?.Disable();
    }

    void OnDestroy()
    {
        GetSystem<LocalPlayerService>()?.Unregister(this);
        if (!_clientSeat)
            return;

        _inputSampler?.Disable();
        for (int i = 0; i < _partyActors.Length; i++)
            _partyActors[i]?.Dispose();
        _partyActors = Array.Empty<CharacterActor>();
        _partyRoots = Array.Empty<GameObject>();
        _partyCoordinator = null;
        simulationHost = null;
    }

    /// <summary>由 CameraManager 暂存 Orbit yaw，下一次采样写入 InputFrame。</summary>
    public void StageMoveReferenceYaw(float yawDegrees)
    {
        if (_inputSampler != null)
            _inputSampler.StageMoveReferenceYaw(yawDegrees);
        else
            Actor?.StageMoveReferenceYaw(yawDegrees);
    }

    /// <summary>把权威槽身份绑定到本机各 Actor；空槽必须对应 Invalid。</summary>
    public void BindPartySimulationInput(
        IReadOnlyList<SimActorId> actorIds,
        InputFrameBuffer inputFrames)
    {
        if (actorIds == null || actorIds.Count != _partyActors.Length)
            throw new ArgumentException("权威阵容身份数量与本机 PartyLoadout 不一致。", nameof(actorIds));
        if (inputFrames == null)
            throw new ArgumentNullException(nameof(inputFrames));

        for (int i = 0; i < _partyActors.Length; i++)
        {
            CharacterActor member = _partyActors[i];
            if (member == null)
            {
                if (actorIds[i].IsValid)
                    throw new InvalidOperationException("本机空槽收到有效权威 ActorId。");
                continue;
            }
            if (!actorIds[i].IsValid)
                throw new InvalidOperationException("本机角色槽缺少有效权威 ActorId。");
            member.BindSimulationInput(actorIds[i], inputFrames);
        }
    }

    /// <summary>客户端预测一次普通切人；新角色贴到旧角色位姿并排队 SwitchIn。</summary>
    public bool TryPredictPartySwitch(long frameIndex)
    {
        if (_partyCoordinator == null
            || !_partyCoordinator.TryResolveSwitchIn(out PartySwitchCommand command))
        {
            return false;
        }

        CharacterActor from = _partyActors[command.FromSlot];
        CharacterActor to = _partyActors[command.ToSlot];
        to.MotorSim.TeleportMm(
            from.MotorSim.PositionMm.X,
            from.MotorSim.YMm,
            from.MotorSim.PositionMm.Z);
        to.AlignSwitchFacing(from.MotorSim.FacingMilliDeg);
        to.AlignSimulationRootToMotor();
        to.SnapPresentationToSimulation();
        from.SetPartyState(PartyMemberState.Exiting);
        to.SetPartyState(PartyMemberState.Active);
        to.QueueExternalIntent(GameplayIntentType.SwitchIn);
        _predictedSwitchFrame = frameIndex;
        _facingDebugVisualizer?.Bind(to);
        return true;
    }

    /// <summary>推进本机全部非空槽；只有 Active 槽接收当帧玩家输入。</summary>
    public void StepPartyPrediction(long frameIndex, float dt, in InputFrame input)
    {
        if (input.WasPressed(InputButton.SwitchCharacter))
            TryPredictPartySwitch(frameIndex);

        InputFrame gameplayInput = input.WithoutButton(InputButton.SwitchCharacter);
        for (int i = 0; i < _partyActors.Length; i++)
        {
            CharacterActor member = _partyActors[i];
            if (member == null)
                continue;
            InputFrame memberInput = i == _partyCoordinator.ActiveIndex
                ? gameplayInput
                : InputFrame.Empty(frameIndex, member.SimulationId);
            member.Step(frameIndex, dt, in memberInput);
            member.ResolvePostCombat(frameIndex);
        }
        CompletePredictedExits();
    }

    /// <summary>渲染 Active 与尚在收招的 Exiting 槽。</summary>
    public void RenderParty(float interpolationAlpha)
    {
        for (int i = 0; i < _partyActors.Length; i++)
            _partyActors[i]?.Render(interpolationAlpha);
    }

    /// <summary>当权威 Active 槽与预测不一致时回滚槽状态；位姿由随后 Owner Snapshot 纠正。</summary>
    public void SynchronizeAuthorityActiveSlot(
        int activeSlot,
        long lastAppliedClientFrameHint)
    {
        if (_partyCoordinator == null)
            return;
        if (_partyCoordinator.ActiveIndex == activeSlot)
        {
            if (_predictedSwitchFrame >= 0
                && lastAppliedClientFrameHint >= _predictedSwitchFrame)
            {
                _predictedSwitchFrame = -1;
            }
            return;
        }
        // 延迟到达的旧快照不能撤销尚未被权威处理的本地切人边沿。
        if (_predictedSwitchFrame >= 0
            && lastAppliedClientFrameHint < _predictedSwitchFrame)
        {
            return;
        }

        _partyCoordinator.SynchronizeActive(activeSlot);
        _predictedSwitchFrame = -1;
        for (int i = 0; i < _partyActors.Length; i++)
        {
            CharacterActor member = _partyActors[i];
            if (member != null)
                member.SetPartyState(_partyCoordinator.States[i]);
        }
        _facingDebugVisualizer?.Bind(Actor);
    }

    /// <summary>本机 Exiting 动作结束后隐藏该槽；与权威帧末规则一致。</summary>
    void CompletePredictedExits()
    {
        for (int i = 0; i < _partyActors.Length; i++)
        {
            CharacterActor member = _partyActors[i];
            if (member == null || member.PartyState != PartyMemberState.Exiting)
                continue;
            if (member.ActionSim.IsActive || member.CurrentState != CharacterStateType.Locomotion)
                continue;
            _partyCoordinator.CompleteExit(i);
            member.SetPartyState(PartyMemberState.Inactive);
        }
    }

    /// <summary>
    /// 本机按 Loadout 装配最多三个 Autonomous Actor；均不进 World、不挂 Hurtbox。
    /// 开局槽可见，其余槽保留独立 Numeric/Action 状态但隐藏。
    /// </summary>
    void BuildClientSeat(InputActionAsset inputActions)
    {
        _clientSeat = true;
        var reader = new InputReader(inputActions);
        GameplayIntentProfile intentProfile = GameplayIntentSettings.Active;
        if (intentProfile == null)
        {
            Debug.LogError("PlayerController: 全局 GameplayIntentProfile 未就绪。", this);
            enabled = false;
            return;
        }

        reader.ConfigureDiscreteInputs(intentProfile.CollectInputReferences());
        _inputSampler = reader;
        CombatWorldController combatWorld = EnsureCombatWorldController();
        simulationHost = combatWorld != null ? combatWorld.EnsureSimulationHost() : null;
        BuildPartyActors(reader);
        GetSystem<LocalPlayerService>()?.Register(this, isLocalOwner: true);
        EnsureFacingDebugVisualizer();
    }

    /// <summary>按槽位创建独立运行时根和 Actor，并将空槽传给纯协调器。</summary>
    void BuildPartyActors(InputReader reader)
    {
        int count = partyLoadout.Count;
        _partyActors = new CharacterActor[count];
        _partyRoots = new GameObject[count];
        var occupied = new bool[count];
        for (int i = 0; i < count; i++)
            occupied[i] = partyLoadout.Members[i] != null;
        _partyCoordinator = new PartyCombatCoordinator(occupied, partyLoadout.StartingSlot);

        for (int i = 0; i < count; i++)
        {
            CharacterDefinition definition = partyLoadout.Members[i];
            if (definition == null)
                continue;

            var slotRoot = new GameObject($"PartySlot_{i}_{definition.Id}");
            slotRoot.transform.SetParent(transform, false);
            _partyRoots[i] = slotRoot;
            CharacterConfig config = definition.CharacterConfig;
            CharacterActor member = CharacterActorFactory.Create(
                slotRoot,
                slotRoot.transform,
                config,
                config.Combat.TeamId,
                reader,
                () => SendQuery(new GetActiveTargetsQuery()),
                null,
                out ActionSim _,
                out CharacterAnimationService _,
                simulationHost != null ? simulationHost.CollisionWorld : null,
                null,
                null,
                ReplicationSeat.Autonomous);
            member.SetPartyState(_partyCoordinator.States[i]);
            _partyActors[i] = member;
        }
    }

    /// <summary>玩家装配前确保场景存在统一战斗世界入口并返回该入口。</summary>
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

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本机玩家阵容运行时：独占三槽 Actor、预测切人、权威槽同步与阵容固定帧推进。
/// </summary>
public sealed class PlayerPartyRuntime : IDisposable, ICharacterActionObserver
{
    readonly PartyLoadout _loadout;
    readonly GameplayIntentProfile _gameplayIntents;
    readonly SimulationHost _simulationHost;
    readonly CharacterActor[] _actors;
    readonly GameObject[] _roots;
    readonly PartyCombatCoordinator _coordinator;
    long _predictedSwitchFrame = -1;

    /// <summary>Active Actor 改变时通知 Controller 刷新相机与调试绑定。</summary>
    public event Action<CharacterActor> ActiveActorChanged;

    /// <summary>当前 Active 角色；队灭或未初始化时为空。</summary>
    public CharacterActor ActiveActor =>
        _coordinator.ActiveIndex >= 0
        && _coordinator.ActiveIndex < _actors.Length
            ? _actors[_coordinator.ActiveIndex]
            : null;

    /// <summary>按 Loadout 槽位对齐的 Actor；空槽对应 null。</summary>
    public IReadOnlyList<CharacterActor> Actors => _actors;

    /// <summary>本机预测支援点口袋。</summary>
    public PartyAssistPoints AssistPoints => _coordinator.AssistPoints;

    /// <summary>当前预测或权威 Active 槽；队灭时为 -1。</summary>
    public int ActiveSlot => _coordinator.ActiveIndex;

    /// <summary>当前 Active 槽的逻辑根；队灭或空槽时为空。</summary>
    public Transform ActiveRoot =>
        ActiveSlot >= 0
        && ActiveSlot < _roots.Length
        && _roots[ActiveSlot] != null
            ? _roots[ActiveSlot].transform
            : null;

    /// <summary>当前 Active 角色定义；队灭时为空。</summary>
    public CharacterDefinition ActiveDefinition =>
        ActiveSlot >= 0 && ActiveSlot < _loadout.Members.Count
            ? _loadout.Members[ActiveSlot]
            : null;

    /// <summary>创建并完整装配本机 Autonomous 阵容；所有 Actor 共用同一设备输入采样器。</summary>
    public PlayerPartyRuntime(
        PartyLoadout loadout,
        GameplayIntentProfile gameplayIntents,
        Transform ownerRoot,
        ILocalInputSampler inputSampler,
        Func<IReadOnlyList<IHurtboxTarget>> activeTargetsProvider,
        SimulationHost simulationHost)
    {
        _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
        _gameplayIntents = gameplayIntents
            ?? throw new ArgumentNullException(nameof(gameplayIntents));
        if (ownerRoot == null)
            throw new ArgumentNullException(nameof(ownerRoot));
        if (inputSampler == null)
            throw new ArgumentNullException(nameof(inputSampler));

        _simulationHost = simulationHost;
        int count = loadout.Count;
        _actors = new CharacterActor[count];
        _roots = new GameObject[count];
        var occupied = new bool[count];
        var assistStyles = new CharacterAssistStyle[count];
        for (int i = 0; i < count; i++)
        {
            CharacterDefinition definition = loadout.Members[i];
            occupied[i] = definition != null;
            assistStyles[i] = definition != null
                ? definition.AssistStyle
                : CharacterAssistStyle.MeleeParry;
        }

        _coordinator = new PartyCombatCoordinator(
            occupied,
            loadout.StartingSlot,
            assistStyles,
            loadout.AssistPointSettings);
        BuildActors(ownerRoot, inputSampler, activeTargetsProvider);
        if (_simulationHost != null)
            _simulationHost.AfterLogicStep += OnAssistParryAuthorityContact;
    }

    /// <summary>把权威槽身份绑定到本机各 Actor；空槽必须对应 Invalid。</summary>
    public void BindSimulationInput(
        IReadOnlyList<SimActorId> actorIds,
        InputFrameBuffer inputFrames)
    {
        if (actorIds == null || actorIds.Count != _actors.Length)
            throw new ArgumentException("权威阵容身份数量与本机 PartyLoadout 不一致。", nameof(actorIds));
        if (inputFrames == null)
            throw new ArgumentNullException(nameof(inputFrames));

        for (int i = 0; i < _actors.Length; i++)
        {
            CharacterActor member = _actors[i];
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

    /// <summary>客户端预测一次切人；有 Cue 时 InstantReplace，否则 DualPresence。</summary>
    public bool TryPredictSwitch(long frameIndex)
    {
        CharacterActor active = ActiveActor;
        if (!_coordinator.CanAcceptGameplayInput
            || active == null
            || active.Vitality.IsDead
            || !_coordinator.TryResolveSwitch(BuildAssistQuery(), out PartySwitchCommand command))
        {
            return false;
        }

        CharacterActor from = _actors[command.FromSlot];
        CharacterActor to = _actors[command.ToSlot];
        if (command.Presentation == PartySwitchPresentation.InstantReplace)
        {
            from.PartyLifecycle.SetState(PartyMemberState.Inactive);
            to.PartyLifecycle.SetState(PartyMemberState.Active);
            if (command.CueOwnerId.IsValid)
                to.ForceSelectTarget(command.CueOwnerId);
            AssistCue cue = default;
            _simulationHost?.AssistCues.TryGetActive(command.CueOwnerId, out cue);
            to.PartyLifecycle.PlaceForAssistSwitchFrom(
                from,
                in cue,
                PartySwitchApplication.UsesEvadeOffset(command.Kind));
            to.PartyLifecycle.QueueExternalIntent(
                PartySwitchApplication.ToIncomingIntent(command.Kind));
        }
        else
        {
            to.PartyLifecycle.PlaceForNormalSwitchFrom(from);
            from.PartyLifecycle.BeginExit();
            to.PartyLifecycle.SetState(PartyMemberState.Active);
            to.PartyLifecycle.QueueExternalIntent(GameplayIntentType.SwitchIn);
        }

        _predictedSwitchFrame = frameIndex;
        ActiveActorChanged?.Invoke(to);
        return true;
    }

    /// <summary>推进本机全部非空槽；只有 Active 槽接收当帧玩家输入。</summary>
    public void StepPrediction(long frameIndex, float fixedDeltaSeconds, in InputFrame input)
    {
        CharacterActor active = ActiveActor;
        bool acceptsGameplay = _coordinator.CanAcceptGameplayInput
            && active != null
            && !active.Vitality.IsDead;
        if (acceptsGameplay && input.WasPressed(InputButton.SwitchCharacter))
            TryPredictSwitch(frameIndex);

        InputFrame gameplayInput = acceptsGameplay
            ? input.WithoutButton(InputButton.SwitchCharacter)
            : InputFrame.Empty(frameIndex, ActiveActor?.SimulationId ?? input.ActorId);
        for (int i = 0; i < _actors.Length; i++)
        {
            CharacterActor member = _actors[i];
            if (member == null)
                continue;
            InputFrame memberInput = i == _coordinator.ActiveIndex
                ? gameplayInput
                : InputFrame.Empty(frameIndex, member.SimulationId);
            member.Step(frameIndex, fixedDeltaSeconds, in memberInput);
            member.ResolvePostCombat(frameIndex);
        }

        ProcessPredictedDeathCloseout();
        CompletePredictedExits();
    }

    /// <summary>渲染 Active 与尚在收招的 Exiting 槽。</summary>
    public void Render(float interpolationAlpha)
    {
        for (int i = 0; i < _actors.Length; i++)
            _actors[i]?.Render(interpolationAlpha);
    }

    /// <summary>权威 Active 槽与预测不一致时回滚槽状态；位姿由随后 Owner Snapshot 纠正。</summary>
    public void SynchronizeActiveSlot(int activeSlot, long lastAppliedClientFrameHint)
    {
        if (_coordinator.ActiveIndex == activeSlot)
        {
            if (_predictedSwitchFrame >= 0
                && lastAppliedClientFrameHint >= _predictedSwitchFrame)
            {
                _predictedSwitchFrame = -1;
            }
            return;
        }
        // 延迟旧快照不得撤销尚未被权威处理的本地切人边沿。
        if (_predictedSwitchFrame >= 0
            && lastAppliedClientFrameHint < _predictedSwitchFrame)
        {
            return;
        }

        _coordinator.SynchronizeActive(activeSlot);
        _predictedSwitchFrame = -1;
        for (int i = 0; i < _actors.Length; i++)
        {
            CharacterActor member = _actors[i];
            if (member != null)
                member.PartyLifecycle.SetState(_coordinator.States[i]);
        }
        ActiveActorChanged?.Invoke(ActiveActor);
    }

    /// <summary>从 Owner 角色快照同步指定稳定槽状态。</summary>
    public void SynchronizeMemberState(SimActorId actorId, int flagsPacked)
    {
        if (!actorId.IsValid)
            return;

        for (int i = 0; i < _actors.Length; i++)
        {
            CharacterActor member = _actors[i];
            if (member == null || member.SimulationId != actorId)
                continue;

            PartyMemberState state = PartyReplicationPacking.ReadMemberState(flagsPacked);
            _coordinator.SynchronizeMemberState(i, state);
            member.PartyLifecycle.SetState(state);
            if (state == PartyMemberState.Active)
                ActiveActorChanged?.Invoke(member);
            return;
        }
    }

    /// <summary>应用权威队灭终态并关闭本地预测输入。</summary>
    public void SynchronizePartyWiped()
    {
        _coordinator.SynchronizePartyWiped();
        _predictedSwitchFrame = -1;
        ActiveActorChanged?.Invoke(null);
    }

    /// <summary>解绑帧事件并释放所有阵容 Actor 的表现资源。</summary>
    public void Dispose()
    {
        if (_simulationHost != null)
            _simulationHost.AfterLogicStep -= OnAssistParryAuthorityContact;
        for (int i = 0; i < _actors.Length; i++)
        {
            CharacterActor member = _actors[i];
            if (member == null)
                continue;
            member.Dispose();
        }
    }

    /// <summary>按槽位创建独立运行时根与 Autonomous Actor，并绑定预测阵容状态。</summary>
    void BuildActors(
        Transform ownerRoot,
        ILocalInputSampler inputSampler,
        Func<IReadOnlyList<IHurtboxTarget>> activeTargetsProvider)
    {
        for (int i = 0; i < _actors.Length; i++)
        {
            CharacterDefinition definition = _loadout.Members[i];
            if (definition == null)
                continue;

            var slotRoot = new GameObject($"PartySlot_{i}_{definition.Id}");
            slotRoot.transform.SetParent(ownerRoot, false);
            _roots[i] = slotRoot;
            CharacterConfig config = definition.CharacterConfig;
            CharacterActor member = CharacterActorFactory.Create(
                slotRoot,
                slotRoot.transform,
                config,
                _gameplayIntents,
                config.Combat.TeamId,
                inputSampler,
                activeTargetsProvider,
                null,
                out ActionSim _,
                out CharacterAnimationService _,
                _simulationHost != null ? _simulationHost.CollisionWorld : null,
                null,
                null,
                ReplicationSeat.Autonomous,
                actionObserver: this);
            member.PartyLifecycle.SetState(_coordinator.States[i]);
            _actors[i] = member;
        }
    }

    /// <summary>读取当前目标与 Host CueBoard，构造纯阵容切换查询。</summary>
    PartyAssistResolveQuery BuildAssistQuery()
    {
        CharacterActor active = ActiveActor;
        bool followUp = active != null && active.Numeric.Flags.HasAssistFollowUp;
        SimActorId preferred = default;
        if (active != null && active.TryGetSelectedTarget(out ITargetable target))
            preferred = target.SimulationId;

        if (_simulationHost != null
            && _simulationHost.AssistCues.TryGetActive(preferred, out AssistCue cue))
        {
            return new PartyAssistResolveQuery(
                true,
                cue.Kind,
                cue.RequiresRanged,
                followUp,
                cue.OwnerId);
        }
        return new PartyAssistResolveQuery(false, AssistCueKind.Gold, false, followUp);
    }

    /// <summary>镜像权威死亡门禁并直接 Dead→Active，禁止复用普通退场路径。</summary>
    void ProcessPredictedDeathCloseout()
    {
        CharacterActor active = ActiveActor;
        if (active == null
            || !_coordinator.TryResolveActiveDeath(
                active.Vitality.IsDead,
                active.DeathSequenceComplete,
                active.DeathActionTotalFrames,
                out PartyDeathCloseout closeout))
        {
            return;
        }

        CharacterActor dead = _actors[closeout.FromSlot];
        dead.PartyLifecycle.SetState(PartyMemberState.Dead);
        if (closeout.PartyWiped)
        {
            ActiveActorChanged?.Invoke(null);
            return;
        }

        CharacterActor incoming = _actors[closeout.ToSlot];
        incoming.PartyLifecycle.PlaceForNormalSwitchFrom(dead);
        incoming.PartyLifecycle.SetState(PartyMemberState.Active);
        incoming.PartyLifecycle.QueueExternalIntent(GameplayIntentType.SwitchIn);
        ActiveActorChanged?.Invoke(incoming);
    }

    /// <summary>本机 Exiting 动作结束后隐藏该槽，与权威帧末规则一致。</summary>
    void CompletePredictedExits()
    {
        for (int i = 0; i < _actors.Length; i++)
        {
            CharacterActor member = _actors[i];
            if (member == null
                || member.PartyLifecycle.State != PartyMemberState.Exiting
                || !member.PartyLifecycle.IsExitReady)
            {
                continue;
            }
            member.PartyLifecycle.CompleteExit();
            _coordinator.CompleteExit(i);
        }
    }

    /// <summary>权威招架接触后镜像 Success 排队与卡肉。</summary>
    void OnAssistParryAuthorityContact(long _)
    {
        IReadOnlyList<AssistParryContact> contacts = _simulationHost.FrameAssistParryContacts;
        for (int c = 0; c < contacts.Count; c++)
        {
            AssistParryContact contact = contacts[c];
            for (int i = 0; i < _actors.Length; i++)
            {
                CharacterActor member = _actors[i];
                if (member == null || member.SimulationId != contact.TargetId)
                    continue;

                member.PartyLifecycle.NotifyAssistParryContact();
                if (contact.HitStopFrames <= 0)
                    continue;
                member.TryRequestHitStopOnCurrentAction(contact.HitStopFrames, oncePerAction: true);
                member.PartyLifecycle.ArmAssistParryHitStopCarry(contact.HitStopFrames);
            }
        }
    }

    /// <summary>终结技起手回复本机预测支援点，与权威口袋规则一致。</summary>
    void ICharacterActionObserver.OnActionBegun(GameplayIntentType intent)
    {
        if (intent == GameplayIntentType.Ultimate)
            _coordinator.AssistPoints.GrantUltimate();
    }
}

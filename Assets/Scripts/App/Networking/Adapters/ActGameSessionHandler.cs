using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>把已通过网络 Session 校验的玩家请求映射为 ACT 权威三槽 Guest 生命周期。</summary>
public sealed class ActGameSessionHandler
{
    readonly ActContentRegistry _content;
    readonly ActGameSessionServices _services;

    /// <summary>创建使用指定内容目录与 App 注册服务的 Gameplay Session Handler。</summary>
    public ActGameSessionHandler(
        ActContentRegistry content,
        ActGameSessionServices services)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// 为加入连接按 Loadout 槽序创建并注册稳定权威 Actor。
    /// 出生位姿由 Match 提供，不再等待 Host Local Actor。
    /// </summary>
    public bool TryCreateGuest(
        PartyLoadout loadout,
        MatchSpawnPose spawn,
        SimulationHost host,
        NetConnectionId connectionId,
        Action prefillEnemyCatalog,
        out ActGameGuest guest,
        CharacterPresentationMode presentation = CharacterPresentationMode.Full)
    {
        guest = null;
        if (loadout == null
            || !loadout.Validate(loadout)
            || host == null
            || !connectionId.IsValid)
            return false;

        Vector3 position = new(
            MotionQuantization.MmToMeters(spawn.XMm),
            MotionQuantization.MmToMeters(spawn.YMm),
            MotionQuantization.MmToMeters(spawn.ZMm));
        Quaternion rotation = Quaternion.Euler(0f, spawn.FacingMilliDeg / 1000f, 0f);
        var gameObject = new GameObject("RemotePlayer");
        gameObject.transform.SetPositionAndRotation(position, rotation);
        RemotePlayerSeat seat = gameObject.AddComponent<RemotePlayerSeat>();
        int count = loadout.Count;
        var occupied = new bool[count];
        for (int i = 0; i < count; i++)
            occupied[i] = loadout.Members[i] != null;
        var coordinator = new PartyCombatCoordinator(occupied, loadout.StartingSlot);
        var members = new ActGameGuestMember[count];

        for (int i = 0; i < count; i++)
        {
            CharacterDefinition definition = loadout.Members[i];
            if (definition == null)
                continue;
            CharacterConfig config = definition.CharacterConfig;
            var slotRoot = new GameObject($"PartySlot_{i}_{definition.Id}");
            slotRoot.transform.SetParent(gameObject.transform, false);
            CharacterActor actor = CharacterActorFactory.Create(
                slotRoot,
                slotRoot.transform,
                config,
                config.Combat.TeamId,
                localInput: null,
                _services.GetActiveTargets,
                host.CombatHits,
                out ActionSim _,
                out CharacterAnimationService animation,
                host.CollisionWorld,
                presentation: presentation);
            actor.SetPartyState(coordinator.States[i]);
            // 玩家站立抗打断同样只读 CombatConfig，与敌人同一 Service。
            // 玩家韧性同样只读 CombatConfig，与敌人同一裁定入口。
            var reactions = new CharacterReactionService(
                actor.Vitality,
                actor,
                new CharacterReactionResolver(config.Combat.Reactions),
                baseInterruptResist: config.Combat.BaseInterruptResist);
            var hurtbox = new CharacterHurtboxTarget(
                slotRoot.transform,
                slotRoot.transform,
                config.Combat.TeamId,
                config.Combat.Hurtbox,
                actor.Vitality,
                actor.ActionSim,
                () => actor.SimulationId,
                actor.MotorSim,
                id => host.LookupNumeric(id),
                () => actor.PartyState == PartyMemberState.Active
                    || actor.PartyState == PartyMemberState.Exiting);

            _services.RegisterCombatActor?.Invoke(slotRoot.transform, actor, animation);
            _services.RegisterTarget?.Invoke(hurtbox);
            actor.Enable();
            SimActorRegistration registration = host.RegisterPlayer(actor);
            host.RegisterNumeric(actor.SimulationId, actor.Numeric);
            _content.PrefillActions(config);
            members[i] = new ActGameGuestMember(
                slotRoot.transform,
                actor,
                registration,
                reactions,
                hurtbox,
                _content.RegisterPlayer(config));
        }

        prefillEnemyCatalog?.Invoke();
        guest = new ActGameGuest(connectionId, seat, coordinator, members);
        ActGameGuestMember activeMember = guest.ActiveMember;
        if (activeMember == null)
            throw new InvalidOperationException("Guest 创建后没有有效 Active 阵容成员。");
        seat.Bind(activeMember.Actor, activeMember.Root);
        _services.RegisterPlayer?.Invoke(seat, false);
        return true;
    }

    /// <summary>按创建的逆序注销并销毁 Guest Gameplay 对象；不操作网络连接表。</summary>
    public void DestroyGuest(ActGameGuest guest, SimulationHost host)
    {
        if (guest == null)
            return;

        _services.UnregisterPlayer?.Invoke(guest.Seat);
        for (int i = guest.Members.Count - 1; i >= 0; i--)
        {
            ActGameGuestMember member = guest.Members[i];
            if (member == null)
                continue;
            _services.UnregisterTarget?.Invoke(member.Hurtbox);
            _services.UnregisterCombatActor?.Invoke(member.Root);
            if (host != null)
                host.Unregister(member.Registration);
            member.Reactions?.Dispose();
            member.Actor?.Dispose();
        }
        if (guest.Seat != null)
            _services.DestroyGameObject?.Invoke(guest.Seat.gameObject);
    }
}

/// <summary>Handler 访问 App Architecture 与 Unity 销毁入口所需的最小服务集合。</summary>
public sealed class ActGameSessionServices
{
    /// <summary>创建 Guest 生命周期所需服务；委托为空时对应注册步骤安全跳过。</summary>
    public ActGameSessionServices(
        Func<IReadOnlyList<IHurtboxTarget>> getActiveTargets,
        Action<Transform, CharacterActor, CharacterAnimationService> registerCombatActor,
        Action<Transform> unregisterCombatActor,
        Action<IHurtboxTarget> registerTarget,
        Action<IHurtboxTarget> unregisterTarget,
        Action<ILocalPlayer, bool> registerPlayer,
        Action<ILocalPlayer> unregisterPlayer,
        Action<GameObject> destroyGameObject)
    {
        GetActiveTargets = getActiveTargets;
        RegisterCombatActor = registerCombatActor;
        UnregisterCombatActor = unregisterCombatActor;
        RegisterTarget = registerTarget;
        UnregisterTarget = unregisterTarget;
        RegisterPlayer = registerPlayer;
        UnregisterPlayer = unregisterPlayer;
        DestroyGameObject = destroyGameObject;
    }

    /// <summary>读取当前可命中目标，供新 Guest Actor 创建 WorldQuery。</summary>
    public Func<IReadOnlyList<IHurtboxTarget>> GetActiveTargets { get; }
    /// <summary>向 App 战斗角色索引登记 Guest。</summary>
    public Action<Transform, CharacterActor, CharacterAnimationService> RegisterCombatActor { get; }
    /// <summary>从 App 战斗角色索引注销 Guest。</summary>
    public Action<Transform> UnregisterCombatActor { get; }
    /// <summary>向 TargetSystem 登记 Guest Hurtbox。</summary>
    public Action<IHurtboxTarget> RegisterTarget { get; }
    /// <summary>从 TargetSystem 注销 Guest Hurtbox。</summary>
    public Action<IHurtboxTarget> UnregisterTarget { get; }
    /// <summary>把 Guest 登记为非本地拥有者玩家。</summary>
    public Action<ILocalPlayer, bool> RegisterPlayer { get; }
    /// <summary>从玩家花名册注销 Guest。</summary>
    public Action<ILocalPlayer> UnregisterPlayer { get; }
    /// <summary>通过 Unity 生命周期入口销毁 Guest GameObject。</summary>
    public Action<GameObject> DestroyGameObject { get; }
}

/// <summary>单个远端连接创建的 ACT 权威阵容；网络状态仍归 ServerSession。</summary>
public sealed class ActGameGuest
{
    readonly ActGameGuestMember[] _members;

    /// <summary>保存 Guest 阵容，供输入路由、复制 Capture 与断线清理。</summary>
    internal ActGameGuest(
        NetConnectionId connectionId,
        RemotePlayerSeat seat,
        PartyCombatCoordinator coordinator,
        ActGameGuestMember[] members)
    {
        ConnectionId = connectionId;
        Seat = seat ?? throw new ArgumentNullException(nameof(seat));
        Coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _members = members ?? throw new ArgumentNullException(nameof(members));
    }

    /// <summary>创建该 Guest 的网络连接。</summary>
    public NetConnectionId ConnectionId { get; }
    /// <summary>供 App 玩家花名册与 Transform 生命周期使用的远端 Seat。</summary>
    public RemotePlayerSeat Seat { get; }
    /// <summary>当前接收输入的权威 CharacterActor。</summary>
    public CharacterActor Actor => ActiveMember?.Actor;
    /// <summary>纯规则阵容协调器。</summary>
    public PartyCombatCoordinator Coordinator { get; }
    /// <summary>按 Loadout 槽序对齐的权威成员。</summary>
    internal IReadOnlyList<ActGameGuestMember> Members => _members;
    /// <summary>当前 Active 成员；非法状态下返回 null。</summary>
    internal ActGameGuestMember ActiveMember =>
        Coordinator.ActiveIndex >= 0 && Coordinator.ActiveIndex < _members.Length
            ? _members[Coordinator.ActiveIndex]
            : null;
    /// <summary>已灌入权威输入缓冲的最新客户端 FrameHint。</summary>
    public long LastAppliedFrameHint { get; set; }

    /// <summary>本逻辑步真正灌入的最新 Hint；无新命令时下行 0。</summary>
    public long AppliedHintThisTick { get; set; }

    /// <summary>执行普通切人，把右侧落点、状态、SwitchIn 意图与 Seat 绑定原子切换。</summary>
    public bool TryResolveSwitch()
    {
        if (!Coordinator.TryResolveSwitchIn(out PartySwitchCommand command))
            return false;

        ActGameGuestMember from = _members[command.FromSlot];
        ActGameGuestMember to = _members[command.ToSlot];
        to.Actor.PlaceForNormalSwitchFrom(from.Actor);
        from.Actor.BeginPartyExit();
        to.Actor.SetPartyState(PartyMemberState.Active);
        to.Actor.QueueExternalIntent(GameplayIntentType.SwitchIn);
        Seat.Bind(to.Actor, to.Root);
        return true;
    }

    /// <summary>帧末把已收完当前动作的 Exiting 成员转入后台。</summary>
    public void CompleteFinishedExits()
    {
        for (int i = 0; i < _members.Length; i++)
        {
            ActGameGuestMember member = _members[i];
            if (member?.Actor == null || member.Actor.PartyState != PartyMemberState.Exiting)
                continue;
            if (!member.Actor.IsPartyExitReady)
                continue;

            member.Actor.CompletePartyExit();
            Coordinator.CompleteExit(i);
        }
    }

    /// <summary>复制按槽序稳定身份；空槽写 Invalid。</summary>
    public SimActorId[] CopyPartyActorIds()
    {
        var ids = new SimActorId[_members.Length];
        for (int i = 0; i < _members.Length; i++)
            ids[i] = _members[i]?.Actor?.SimulationId ?? SimActorId.Invalid;
        return ids;
    }
}

/// <summary>权威阵容单槽拥有的 Actor、注册句柄与表现/受击生命周期。</summary>
internal sealed class ActGameGuestMember
{
    /// <summary>保存一个已完整注册的阵容槽。</summary>
    public ActGameGuestMember(
        Transform root,
        CharacterActor actor,
        SimActorRegistration registration,
        CharacterReactionService reactions,
        CharacterHurtboxTarget hurtbox,
        NetArchetypeId archetypeId)
    {
        Root = root;
        Actor = actor;
        Registration = registration;
        Reactions = reactions;
        Hurtbox = hurtbox;
        ArchetypeId = archetypeId;
    }

    /// <summary>App 战斗索引使用的槽根。</summary>
    public Transform Root { get; }
    /// <summary>槽对应的稳定权威 Actor。</summary>
    public CharacterActor Actor { get; }
    /// <summary>从 SimulationWorld 注销所需句柄。</summary>
    public SimActorRegistration Registration { get; }
    /// <summary>受击反应订阅。</summary>
    public CharacterReactionService Reactions { get; }
    /// <summary>TargetSystem 受击目标。</summary>
    public CharacterHurtboxTarget Hurtbox { get; }
    /// <summary>槽角色的稳定网络原型。</summary>
    public NetArchetypeId ArchetypeId { get; }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>把已通过网络 Session 校验的玩家请求映射为 ACT 权威三槽 Guest 生命周期。</summary>
public sealed class ActGameSessionHandler
{
    readonly GameContentCatalog _content;
    readonly ActGameSessionServices _services;

    /// <summary>创建使用指定内容目录与 App 注册服务的 Gameplay Session Handler。</summary>
    public ActGameSessionHandler(
        GameContentCatalog content,
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
        var assistStyles = new CharacterAssistStyle[count];
        for (int i = 0; i < count; i++)
        {
            occupied[i] = loadout.Members[i] != null;
            assistStyles[i] = loadout.Members[i] != null
                ? loadout.Members[i].AssistStyle
                : CharacterAssistStyle.MeleeParry;
        }
        var coordinator = new PartyCombatCoordinator(
            occupied,
            loadout.StartingSlot,
            assistStyles,
            loadout.AssistPointSettings);
        var actionObserver = new GuestActionObserver(coordinator);
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
                _content.GameplayIntents,
                config.Combat.TeamId,
                localInput: null,
                _services.GetActiveTargets,
                host.CombatHits,
                out ActionSim _,
                out CharacterAnimationService animation,
                host.CollisionWorld,
                presentation: presentation,
                actionObserver: actionObserver);
            actor.PartyLifecycle.SetState(coordinator.States[i]);
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
                () => actor.PartyLifecycle.State == PartyMemberState.Active
                    || actor.PartyLifecycle.State == PartyMemberState.Exiting);

            _services.RegisterCombatActor?.Invoke(slotRoot.transform, actor, animation);
            _services.RegisterTarget?.Invoke(hurtbox);
            actor.Enable();
            SimActorRegistration registration = host.RegisterPlayer(actor);
            host.RegisterNumeric(actor.SimulationId, actor.Numeric);
            host.RegisterCombatParticipant(actor, reactions);
            members[i] = new ActGameGuestMember(
                slotRoot.transform,
                actor,
                registration,
                reactions,
                hurtbox,
                _content.GetArchetypeId(config));
        }

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

/// <summary>把权威角色动作边沿映射为阵容资源规则。</summary>
internal sealed class GuestActionObserver : ICharacterActionObserver
{
    readonly PartyCombatCoordinator _coordinator;

    /// <summary>绑定目标权威阵容协调器。</summary>
    public GuestActionObserver(PartyCombatCoordinator coordinator) =>
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

    /// <inheritdoc />
    public void OnActionBegun(GameplayIntentType intent)
    {
        if (intent == GameplayIntentType.Ultimate)
            _coordinator.AssistPoints.GrantUltimate();
    }
}

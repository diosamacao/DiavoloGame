using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>把已通过网络 Session 校验的玩家请求映射为 ACT 权威 Guest Actor 生命周期。</summary>
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
    /// 为加入连接创建并注册权威 Guest Actor。
    /// 出生位姿由 Match 提供，不再等待 Host Local Actor。
    /// </summary>
    public bool TryCreateGuest(
        CharacterConfig config,
        in MatchSpawnPose spawn,
        SimulationHost host,
        NetConnectionId connectionId,
        Action prefillEnemyCatalog,
        out ActGameGuest guest)
    {
        guest = null;
        if (config == null || host == null || !connectionId.IsValid)
            return false;

        Vector3 position = new(
            MotionQuantization.MmToMeters(spawn.XMm),
            MotionQuantization.MmToMeters(spawn.YMm),
            MotionQuantization.MmToMeters(spawn.ZMm));
        Quaternion rotation = Quaternion.Euler(0f, spawn.FacingMilliDeg / 1000f, 0f);
        var gameObject = new GameObject("RemotePlayer");
        gameObject.transform.SetPositionAndRotation(position, rotation);

        RemotePlayerSeat seat = gameObject.AddComponent<RemotePlayerSeat>();
        CharacterActor actor = CharacterActorFactory.Create(
            gameObject,
            gameObject.transform,
            config,
            config.Combat.TeamId,
            localInput: null,
            _services.GetActiveTargets,
            host.CombatHits,
            out ActionSim _,
            out CharacterAnimationService animation,
            host.CollisionWorld);
        seat.Bind(actor);

        var reactions = new CharacterReactionService(
            actor.Vitality,
            actor,
            new CharacterReactionResolver(config.Combat.Reactions));
        var hurtbox = new CharacterHurtboxTarget(
            gameObject.transform,
            gameObject.transform,
            config.Combat.TeamId,
            config.Combat.Hurtbox,
            actor.Vitality,
            actor.ActionSim,
            () => actor.SimulationId,
            actor.MotorSim,
            id => host.LookupNumeric(id));

        _services.RegisterCombatActor?.Invoke(gameObject.transform, actor, animation);
        _services.RegisterTarget?.Invoke(hurtbox);
        _services.RegisterPlayer?.Invoke(seat, false);

        actor.Enable();
        SimActorRegistration registration = host.RegisterPlayer(actor);
        host.RegisterNumeric(actor.SimulationId, actor.Numeric);

        _content.PrefillActions(config);
        prefillEnemyCatalog?.Invoke();
        NetArchetypeId archetypeId = _content.RegisterPlayer(config);
        guest = new ActGameGuest(
            connectionId,
            seat,
            actor,
            registration,
            reactions,
            hurtbox,
            archetypeId);
        return true;
    }

    /// <summary>按创建的逆序注销并销毁 Guest Gameplay 对象；不操作网络连接表。</summary>
    public void DestroyGuest(ActGameGuest guest, SimulationHost host)
    {
        if (guest == null)
            return;

        _services.UnregisterPlayer?.Invoke(guest.Seat);
        _services.UnregisterTarget?.Invoke(guest.Hurtbox);
        _services.UnregisterCombatActor?.Invoke(
            guest.Seat != null ? guest.Seat.transform : null);
        if (host != null)
            host.Unregister(guest.Registration);
        guest.Reactions?.Dispose();
        guest.Actor?.Dispose();
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

/// <summary>单个远端连接创建的 ACT 权威对象集合；网络状态仍归 ServerSession。</summary>
public sealed class ActGameGuest
{
    /// <summary>保存 Guest 创建结果，供 Room Accept、输入路由、复制 Capture 与断线清理。</summary>
    public ActGameGuest(
        NetConnectionId connectionId,
        RemotePlayerSeat seat,
        CharacterActor actor,
        SimActorRegistration registration,
        CharacterReactionService reactions,
        CharacterHurtboxTarget hurtbox,
        NetArchetypeId archetypeId)
    {
        ConnectionId = connectionId;
        Seat = seat;
        Actor = actor;
        Registration = registration;
        Reactions = reactions;
        Hurtbox = hurtbox;
        ArchetypeId = archetypeId;
    }

    /// <summary>创建该 Guest 的网络连接。</summary>
    public NetConnectionId ConnectionId { get; }
    /// <summary>供 App 玩家花名册与 Transform 生命周期使用的远端 Seat。</summary>
    public RemotePlayerSeat Seat { get; }
    /// <summary>Host 世界中的权威 CharacterActor。</summary>
    public CharacterActor Actor { get; }
    /// <summary>从 SimulationHost 注销 Actor 所需句柄。</summary>
    public SimActorRegistration Registration { get; }
    /// <summary>Guest 受击反应服务，销毁时必须释放订阅。</summary>
    public CharacterReactionService Reactions { get; }
    /// <summary>注册到 Host TargetSystem 的权威 Hurtbox。</summary>
    public CharacterHurtboxTarget Hurtbox { get; }
    /// <summary>Guest 复用 Host 玩家配置得到的稳定网络原型。</summary>
    public NetArchetypeId ArchetypeId { get; }
    /// <summary>已灌入权威输入缓冲的最新客户端 FrameHint。</summary>
    public long LastAppliedFrameHint { get; set; }

    /// <summary>本逻辑步真正灌入的最新 Hint；无新命令时下行 0。</summary>
    public long AppliedHintThisTick { get; set; }
}

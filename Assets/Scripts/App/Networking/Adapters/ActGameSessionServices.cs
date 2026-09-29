using System;
using System.Collections.Generic;
using UnityEngine;

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

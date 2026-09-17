using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>客户端复制反馈协调器：独占命中 Cue、弹刀竞态、预测卡肉与 Owner 软体分离。</summary>
public sealed class ReplicatedFeedbackCoordinator
{
    readonly CombatWorldController _world;
    readonly GameContentCatalog _content;
    readonly ActObserverReplicationAdapter _observer;
    readonly HashSet<SimHitKey> _playedHits = new();
    readonly List<SimHitKey> _playedHitOrder = new();
    readonly List<RemoteCharacterProxy> _softBlockers = new();
    readonly ActReplicationEventCodec.OwnerAssistParryEventQueue _assistParryEvents = new();
    SimActorId[] _ownerPartyActorIds = Array.Empty<SimActorId>();
    SimVec2[] _softBlockerPosMm = Array.Empty<SimVec2>();
    int[] _softBlockerRadiiMm = Array.Empty<int>();
    PlayerController _localPlayer;
    int _lastPresentedHitStopFrames;
    bool _authorityRosterReady;

    /// <summary>绑定世界表现组件、动作目录与 Observer Proxy 注册表。</summary>
    public ReplicatedFeedbackCoordinator(
        CombatWorldController world,
        GameContentCatalog content,
        ActObserverReplicationAdapter observer)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
    }

    /// <summary>新 Session 开始时清空跨通道弹刀暂存，并登记初始 Owner 身份。</summary>
    public void BeginSession(
        IReadOnlyList<SimActorId> actorIds,
        PlayerController localPlayer)
    {
        _assistParryEvents.Clear();
        UpdateOwnerRoster(actorIds, false, localPlayer);
    }

    /// <summary>更新 Owner 阵容身份；权威 Meta 就绪时重试可靠通道早到的弹刀事件。</summary>
    public void UpdateOwnerRoster(
        IReadOnlyList<SimActorId> actorIds,
        bool authorityRosterReady,
        PlayerController localPlayer)
    {
        _ownerPartyActorIds = CopyIds(actorIds);
        _authorityRosterReady = authorityRosterReady;
        _localPlayer = localPlayer;
        if (_authorityRosterReady)
            _assistParryEvents.Retry(_ownerPartyActorIds, TryApplyOwnerAssistParry);
    }

    /// <summary>解码并播放可靠权威命中；按 SimHitKey 去重。</summary>
    public void ApplyReplicationEvents(byte[] body)
    {
        ReplicatedHitEvent[] hits = ActReplicationEventCodec.Decode(body);
        PlayReplicatedHits(hits);
    }

    /// <summary>本机 FreezeFrames 升高时同步 VFX 卡肉，不发布 AttackHitEvent。</summary>
    public void PresentPredictedHitStop(CharacterActor actor, PlayerController localPlayer)
    {
        if (actor?.ActionSim == null)
            return;
        int freeze = actor.ActionSim.FreezeFrames;
        if (freeze <= _lastPresentedHitStopFrames)
        {
            _lastPresentedHitStopFrames = freeze;
            return;
        }

        HitStopController hitStop = _world.GetComponent<HitStopController>();
        hitStop?.PresentPredicted(localPlayer != null ? localPlayer.Root : null, freeze);
        _lastPresentedHitStopFrames = freeze;
    }

    /// <summary>本机对只读 Observer 做单向软弹开，不把 Proxy 推回权威世界。</summary>
    public void ResolveAutonomousSoftBody(CharacterActor actor)
    {
        if (actor == null || !actor.ParticipatesInSoftBodySeparation)
            return;

        _softBlockers.Clear();
        foreach (RemoteCharacterProxy proxy in _observer.Proxies)
        {
            if (proxy == null || !proxy.IsAlive || proxy.MotorSim == null)
                continue;
            _softBlockers.Add(proxy);
        }
        if (_softBlockers.Count == 0)
            return;

        _softBlockers.Sort(CompareProxyId);
        if (_softBlockerPosMm.Length < _softBlockers.Count)
        {
            _softBlockerPosMm = new SimVec2[_softBlockers.Count];
            _softBlockerRadiiMm = new int[_softBlockers.Count];
        }
        for (int i = 0; i < _softBlockers.Count; i++)
        {
            CharacterMotorSim motor = _softBlockers[i].MotorSim;
            _softBlockerPosMm[i] = motor.PositionMm;
            _softBlockerRadiiMm[i] = motor.RadiusMm;
        }

        if (AutonomousSoftBodySolver.TrySeparateLocal(
                actor.MotorSim,
                _softBlockerPosMm,
                _softBlockerRadiiMm,
                _softBlockers.Count))
        {
            actor.OnSoftBodySeparationApplied();
        }
    }

    /// <summary>Recovery 时清空跨通道弹刀队列并等待新 Meta 重新开闸。</summary>
    public void ResetForRecovery()
    {
        _assistParryEvents.Clear();
        _authorityRosterReady = false;
    }

    /// <summary>房间关闭时清空命中去重、弹刀队列与 Owner 阵容引用。</summary>
    public void Shutdown()
    {
        _playedHits.Clear();
        _playedHitOrder.Clear();
        _assistParryEvents.Clear();
        _ownerPartyActorIds = Array.Empty<SimActorId>();
        _localPlayer = null;
        _authorityRosterReady = false;
    }

    /// <summary>按权威落点播 Cue；Flinch 对 Observer 叠 Additive，不写 ActionSim。</summary>
    void PlayReplicatedHits(ReplicatedHitEvent[] hits)
    {
        if (hits == null || hits.Length == 0)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            ReplicatedHitEvent hit = hits[i];
            if (hit.AbsorbedByAssistParry)
            {
                // Event 与 Snapshot 分通道；阵容身份未绑定前必须暂存，不能提前去重丢弃。
                _assistParryEvents.ApplyOrPend(
                    in hit,
                    _ownerPartyActorIds,
                    _authorityRosterReady,
                    TryApplyOwnerAssistParry);
                continue;
            }
            if (!RememberHit(hit.Key))
                continue;

            Transform attackerRoot = ResolveProxyRoot(hit.Key.AttackerId);
            HitImpactCuePlayer.TryPlay(
                _content.Actions,
                hit.ActionId,
                hit.Key.HitboxIndex,
                new Vector3(
                    MotionQuantization.MmToMeters(hit.HitXMm),
                    MotionQuantization.MmToMeters(hit.HitYMm),
                    MotionQuantization.MmToMeters(hit.HitZMm)),
                new Vector3(
                    MotionQuantization.MmToMeters(hit.DirXMm),
                    0f,
                    MotionQuantization.MmToMeters(hit.DirZMm)),
                attackerRoot);

            if (!_observer.TryGetProxy(hit.Key.TargetId, out RemoteCharacterProxy proxy))
                continue;
            proxy.NotifyReplicatedReaction(hit.ReactionKind);
            if (hit.ReactionKind != HitReactionKind.Flinch)
                continue;

            HitFlinchPlaybackController flinchPlayback =
                _world.GetComponent<HitFlinchPlaybackController>();
            if (flinchPlayback != null)
                flinchPlayback.TryPlayOnProxy(proxy, AnimationKey.HitShake);
            else
                HitFlinchPresentation.TryPlayOnProxy(
                    proxy,
                    AnimationKey.HitShake,
                    fallbackClip: null,
                    fallbackMask: null,
                    fadeDuration: 0.05f);
        }
    }

    /// <summary>按稳定 SimulationId 把弹刀接触应用到 Owner 任意阵容槽。</summary>
    bool TryApplyOwnerAssistParry(ReplicatedHitEvent hit)
    {
        IReadOnlyList<CharacterActor> party = _localPlayer?.Party?.Actors;
        if (party == null)
            return false;
        for (int i = 0; i < party.Count; i++)
        {
            CharacterActor actor = party[i];
            if (actor == null || actor.SimulationId != hit.Key.TargetId)
                continue;
            actor.PartyLifecycle.NotifyAssistParryContact();
            actor.PartyLifecycle.ArmAssistParryHitStopCarry(hit.HitStopFrames);
            return true;
        }
        return false;
    }

    /// <summary>记录已播放命中并限制去重窗口大小。</summary>
    bool RememberHit(SimHitKey key)
    {
        if (!_playedHits.Add(key))
            return false;
        _playedHitOrder.Add(key);
        while (_playedHitOrder.Count > 128)
        {
            _playedHits.Remove(_playedHitOrder[0]);
            _playedHitOrder.RemoveAt(0);
        }
        return true;
    }

    /// <summary>解析攻击者表现根：Owner 使用预测体，Observer 使用 Proxy 根。</summary>
    Transform ResolveProxyRoot(SimActorId actorId)
    {
        if (!actorId.IsValid)
            return null;
        for (int i = 0; i < _ownerPartyActorIds.Length; i++)
        {
            if (_ownerPartyActorIds[i] != actorId)
                continue;
            CharacterActor member = _localPlayer?.Party != null
                && i < _localPlayer.Party.Actors.Count
                    ? _localPlayer.Party.Actors[i]
                    : null;
            return member?.PresentationRoot != null
                ? member.PresentationRoot
                : _localPlayer != null ? _localPlayer.Root : null;
        }
        return _observer.TryGetProxy(actorId, out RemoteCharacterProxy proxy)
            ? proxy.Root
            : null;
    }

    static int CompareProxyId(RemoteCharacterProxy left, RemoteCharacterProxy right) =>
        left.SimulationId.Value.CompareTo(right.SimulationId.Value);

    static SimActorId[] CopyIds(IReadOnlyList<SimActorId> actorIds)
    {
        if (actorIds == null || actorIds.Count == 0)
            return Array.Empty<SimActorId>();
        var copy = new SimActorId[actorIds.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = actorIds[i];
        return copy;
    }
}

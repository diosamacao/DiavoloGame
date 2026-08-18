using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Dedicated 权威世界：Headless Actor、命令灌入与外部时钟步进。</summary>
public sealed class DedicatedAuthorityWorld : IDedicatedAuthorityWorld
{
    readonly SimulationHost _host;
    readonly ActContentRegistry _content;
    readonly ActGameSessionHandler _gameSession;
    readonly ActAuthorityReplicationAdapter _authority;
    readonly ServerSimulationRunner _runner;
    readonly Dictionary<NetConnectionId, ActGameGuest> _guests = new();
    readonly Dictionary<NetConnectionId, long> _lastHints = new();

    /// <summary>绑定已关闭自动 Tick 的 SimulationHost 与场景内容 Registry。</summary>
    public DedicatedAuthorityWorld(
        SimulationHost host,
        ACTGameArchitecture architecture,
        ActContentRegistry content)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _content = content ?? throw new ArgumentNullException(nameof(content));
        if (architecture == null)
            throw new ArgumentNullException(nameof(architecture));

        _host.DriveFromExternalClock = true;
        _gameSession = new ActGameSessionHandler(content, CreateServices(architecture));
        _authority = new ActAuthorityReplicationAdapter(content);
        var kernel = new SimulationStepKernel();
        _runner = new ServerSimulationRunner(kernel, StepOnce);
    }

    /// <inheritdoc />
    public long CurrentFrame => _host.CurrentFrame;

    /// <summary>最近一次 Runner 指标，供测试读取 overrun。</summary>
    public SimulationTickMetrics TickMetrics => _runner.Metrics;

    /// <inheritdoc />
    public bool TryAcceptPlayer(in MatchPlayerSlot slot)
    {
        if (!_content.TryGetAnyPlayerConfig(out CharacterConfig config)
            && slot.ArchetypeId.IsValid)
        {
            try
            {
                config = _content.ResolveCharacterConfig(slot.ArchetypeId);
            }
            catch (KeyNotFoundException)
            {
                config = null;
            }
        }

        MatchSpawnPose spawn = slot.Spawn;
        if (config == null
            || !_gameSession.TryCreateGuest(
                config,
                spawn,
                _host,
                slot.ConnectionId,
                prefillEnemyCatalog: null,
                out ActGameGuest guest,
                CharacterPresentationMode.AuthorityHeadless))
        {
            return false;
        }

        _guests[slot.ConnectionId] = guest;
        _lastHints[slot.ConnectionId] = 0;
        return true;
    }

    /// <inheritdoc />
    public void ApplyCommands(NetConnectionId connectionId, ClientCommand[] commands)
    {
        if (!_guests.TryGetValue(connectionId, out ActGameGuest guest)
            || guest.Actor == null
            || _host.World == null)
        {
            return;
        }

        _lastHints.TryGetValue(connectionId, out long lastHint);
        ActAuthorityInputApplyResult result = _authority.ApplyGuestCommands(
            _host.World.InputFrames,
            _host.CurrentFrame,
            guest.Actor.SimulationId,
            commands,
            lastHint);
        if (result.Applied)
            _lastHints[connectionId] = result.NewestHint;
    }

    /// <inheritdoc />
    public void RemovePlayer(NetConnectionId connectionId)
    {
        if (_guests.TryGetValue(connectionId, out ActGameGuest guest))
        {
            _guests.Remove(connectionId);
            _gameSession.DestroyGuest(guest, _host);
        }

        _lastHints.Remove(connectionId);
    }

    /// <inheritdoc />
    public void Advance(long nowMs) => _runner.Advance(nowMs);

    /// <inheritdoc />
    public void Dispose()
    {
        var ids = new List<NetConnectionId>(_guests.Keys);
        for (int i = 0; i < ids.Count; i++)
            RemovePlayer(ids[i]);
    }

    /// <summary>与 Listen Host 同一帧序：Sample → BeginFrame → Step → 结算 → PostCombat。</summary>
    void StepOnce() => _host.StepOnce();

    static ActGameSessionServices CreateServices(ACTGameArchitecture architecture)
    {
        return new ActGameSessionServices(
            () => architecture.SendQuery(new GetActiveTargetsQuery()),
            (root, actor, animation) =>
                architecture.GetSystem<CombatActorSystem>()?.Register(root, actor, animation),
            root => architecture.GetSystem<CombatActorSystem>()?.Unregister(root),
            target => architecture.GetSystem<TargetSystem>()?.Register(target),
            target => architecture.GetSystem<TargetSystem>()?.Unregister(target),
            (player, isLocalOwner) =>
                architecture.GetSystem<LocalPlayerService>()?.Register(player, isLocalOwner),
            player => architecture.GetSystem<LocalPlayerService>()?.Unregister(player),
            gameObject => UnityEngine.Object.Destroy(gameObject));
    }
}

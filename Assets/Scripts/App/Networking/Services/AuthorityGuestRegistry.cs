using System;
using System.Collections.Generic;

/// <summary>Dedicated Guest 注册表：独占 Headless 阵容创建、连接映射与对称销毁。</summary>
public sealed class AuthorityGuestRegistry : IDisposable
{
    readonly SimulationHost _host;
    readonly GameContentCatalog _content;
    readonly ActGameSessionHandler _gameSession;
    readonly Dictionary<NetConnectionId, ActGameGuest> _guests = new();

    /// <summary>创建绑定到指定 Host 与 Architecture 服务的权威 Guest 注册表。</summary>
    public AuthorityGuestRegistry(
        SimulationHost host,
        ACTGameArchitecture architecture,
        GameContentCatalog content)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _content = content ?? throw new ArgumentNullException(nameof(content));
        if (architecture == null)
            throw new ArgumentNullException(nameof(architecture));
        _gameSession = new ActGameSessionHandler(content, CreateServices(architecture));
    }

    /// <summary>当前连接到 Guest 的只读枚举；调用期间禁止修改注册表。</summary>
    public IEnumerable<KeyValuePair<NetConnectionId, ActGameGuest>> Entries => _guests;

    /// <summary>当前 Guest 数量。</summary>
    public int Count => _guests.Count;

    /// <summary>按 Match 槽位创建 Headless Authority 阵容并登记连接。</summary>
    public bool TryAcceptPlayer(in MatchPlayerSlot slot, out NetEntityId entityId)
    {
        entityId = NetEntityId.Invalid;
        PartyLoadout loadout = _content.PlayerLoadout;
        if (loadout == null
            || !_gameSession.TryCreateGuest(
                loadout,
                slot.Spawn,
                _host,
                slot.ConnectionId,
                out ActGameGuest guest,
                CharacterPresentationMode.AuthorityHeadless)
            || guest.Actor == null
            || !guest.Actor.SimulationId.IsValid)
        {
            return false;
        }

        _guests[slot.ConnectionId] = guest;
        entityId = new NetEntityId(guest.Actor.SimulationId.Value);
        return true;
    }

    /// <summary>按连接获取权威 Guest。</summary>
    public bool TryGet(NetConnectionId connectionId, out ActGameGuest guest) =>
        _guests.TryGetValue(connectionId, out guest);

    /// <summary>只移除该连接的权威阵容，不影响其他 Guest。</summary>
    public void Remove(NetConnectionId connectionId)
    {
        if (!_guests.TryGetValue(connectionId, out ActGameGuest guest))
            return;
        _guests.Remove(connectionId);
        _gameSession.DestroyGuest(guest, _host);
    }

    /// <summary>把当前 Guest 复制到可复用列表，供同帧 Capture 使用。</summary>
    public void CopyTo(List<ActGameGuest> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));
        results.Clear();
        foreach (ActGameGuest guest in _guests.Values)
            results.Add(guest);
    }

    /// <summary>在逻辑帧结算后推进每个 Guest 的死亡接替与普通退场。</summary>
    public void AdvancePostLogicLifecycles()
    {
        foreach (ActGameGuest guest in _guests.Values)
        {
            guest.ProcessDeathCloseoutAfterLogicStep();
            guest.CompleteFinishedExits();
        }
    }

    /// <summary>销毁全部 Guest 并清空连接映射。</summary>
    public void Dispose()
    {
        var ids = new List<NetConnectionId>(_guests.Keys);
        for (int i = 0; i < ids.Count; i++)
            Remove(ids[i]);
    }

    /// <summary>把 Architecture 系统能力收敛为 Guest 工厂所需服务集合。</summary>
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

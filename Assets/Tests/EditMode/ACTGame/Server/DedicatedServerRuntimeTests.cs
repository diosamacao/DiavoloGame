using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>W5/W7/W8 Dedicated Runtime：Match 生命周期、每连接 Frame，以及空房/对局结束退出。</summary>
public sealed class DedicatedServerRuntimeTests
{
    static readonly NetEndpoint Endpoint = new("dedicated-loopback", 7777);

    /// <summary>非法配置不得绑端口，退出码为 ConfigFailed。</summary>
    [Test]
    public void InvalidConfig_ReturnsConfigFailedWithoutListening()
    {
        var config = new ServerLaunchConfig(
            "",
            -1,
            contentVersion: 1,
            maxPlayers: 4,
            idleTimeoutMs: 10000,
            heartbeatIntervalMs: 500,
            new NetworkProtocolVersion(1),
            default);
        var transport = new LoopbackTransport(new LoopbackNetwork());

        DedicatedServerRuntime runtime = DedicatedServerRuntime.TryStart(
            transport,
            config,
            new StubAuthorityWorld(),
            out ServerExitCode exit);

        Assert.That(runtime, Is.Null);
        Assert.That(exit, Is.EqualTo(ServerExitCode.ConfigFailed));
    }

    /// <summary>空房超时不能为负数，否则视为配置失败。</summary>
    [Test]
    public void NegativeEmptyLobbyTimeout_ReturnsConfigFailed()
    {
        ServerLaunchConfig config = ServerLaunchConfig.CreateDefault(
            7777,
            contentVersion: 1,
            emptyLobbyTimeoutMs: -1);
        DedicatedServerRuntime runtime = DedicatedServerRuntime.TryStart(
            new LoopbackTransport(new LoopbackNetwork()),
            config,
            new StubAuthorityWorld(),
            out ServerExitCode exit);

        Assert.That(runtime, Is.Null);
        Assert.That(exit, Is.EqualTo(ServerExitCode.ConfigFailed));
    }

    /// <summary>Transport 绑端口抛错时退出码为 BindFailed。</summary>
    [Test]
    public void BindFailure_ReturnsBindFailed()
    {
        ServerLaunchConfig config = ServerLaunchConfig.CreateDefault(7777, contentVersion: 1);
        DedicatedServerRuntime runtime = DedicatedServerRuntime.TryStart(
            new ThrowingBindTransport(),
            config,
            new StubAuthorityWorld(),
            out ServerExitCode exit);

        Assert.That(runtime, Is.Null);
        Assert.That(exit, Is.EqualTo(ServerExitCode.BindFailed));
    }

    /// <summary>无 LocalPlayer 的 Dedicated 可 Listening 并接纳第一名玩家。</summary>
    [Test]
    public void Start_WithoutLocalPlayer_ListensAndAcceptsFirstClient()
    {
        using var harness = new DedicatedHarness(maxPlayers: 3);
        ClientSession client = harness.JoinClients(1)[0];

        Assert.That(harness.Runtime.ProcessRole, Is.EqualTo(NetProcessRole.DedicatedServer));
        Assert.That(harness.Runtime.LocalPlayerCount, Is.EqualTo(0));
        Assert.That(harness.Runtime.IsListening, Is.True);
        Assert.That(harness.Runtime.IsReady, Is.True);
        Assert.That(harness.Runtime.ShouldExit, Is.False);
        Assert.That(harness.Runtime.ExitCode, Is.EqualTo(ServerExitCode.Success));
        Assert.That(client.State, Is.EqualTo(ClientSessionState.Joined));
        Assert.That(client.JoinAccept.AuthorityEntityId.IsValid, Is.False);
        Assert.That(client.JoinAccept.EntityId.IsValid, Is.True);
        Assert.That(harness.Runtime.JoinedPlayerCount, Is.EqualTo(1));
        Assert.That(harness.Runtime.MatchPhase, Is.EqualTo(DedicatedMatchPhase.Playing));
    }

    /// <summary>三个 Loopback Client 获得不同 PlayerId / EntityId。</summary>
    [Test]
    public void ThreeClients_ReceiveDistinctPlayerAndEntityIds()
    {
        using var harness = new DedicatedHarness(maxPlayers: 3);
        ClientSession[] clients = harness.JoinClients(3);

        var playerIds = new HashSet<int>();
        var entityIds = new HashSet<int>();
        var spawnXs = new HashSet<int>();
        for (int i = 0; i < clients.Length; i++)
        {
            Assert.That(clients[i].State, Is.EqualTo(ClientSessionState.Joined));
            playerIds.Add(clients[i].JoinAccept.PlayerId.Value);
            entityIds.Add(clients[i].JoinAccept.EntityId.Value);
            Assert.That(
                harness.Runtime.TryGetPlayer(
                    harness.ConnectionOf(clients[i]),
                    out DedicatedPlayerRuntime player),
                Is.True);
            spawnXs.Add(player.Slot.Spawn.XMm);
        }

        Assert.That(playerIds, Has.Count.EqualTo(3));
        Assert.That(entityIds, Has.Count.EqualTo(3));
        Assert.That(spawnXs, Has.Count.EqualTo(3));
        Assert.That(harness.Runtime.Match.PlayerCount, Is.EqualTo(3));
    }

    /// <summary>W9：本机 Client 断开不得带走其余 Guest（Listen Local 与远端同一 ServerRuntime）。</summary>
    [Test]
    public void LocalClientDisconnect_DoesNotDestroyRemainingGuest()
    {
        using var harness = new DedicatedHarness(maxPlayers: 3);
        ClientSession[] clients = harness.JoinClients(2);
        NetConnectionId local = harness.ConnectionOf(clients[0]);
        NetConnectionId guest = harness.ConnectionOf(clients[1]);

        clients[0].Dispose();
        harness.Runtime.Poll(1);
        clients[1].Poll(1);

        Assert.That(harness.Runtime.TryGetPlayer(local, out _), Is.False);
        Assert.That(harness.Runtime.TryGetPlayer(guest, out _), Is.True);
        Assert.That(clients[1].State, Is.EqualTo(ClientSessionState.Joined));
        Assert.That(harness.Runtime.JoinedPlayerCount, Is.EqualTo(1));
    }

    /// <summary>一人断开不影响其余玩家的 Session 与 Match 槽位。</summary>
    [Test]
    public void DisconnectOne_DoesNotRemoveOthers()
    {
        using var harness = new DedicatedHarness(maxPlayers: 3);
        ClientSession[] clients = harness.JoinClients(3);
        NetConnectionId dropped = harness.ConnectionOf(clients[1]);

        harness.Runtime.Session.Disconnect(dropped, DisconnectReason.ServerShutdown);
        clients[1].Poll(1);
        harness.Runtime.Poll(1);

        Assert.That(clients[1].State, Is.EqualTo(ClientSessionState.Ended));
        Assert.That(clients[0].State, Is.EqualTo(ClientSessionState.Joined));
        Assert.That(clients[2].State, Is.EqualTo(ClientSessionState.Joined));
        Assert.That(harness.Runtime.JoinedPlayerCount, Is.EqualTo(2));
        Assert.That(harness.Runtime.TryGetPlayer(dropped, out _), Is.False);
        Assert.That(harness.Runtime.TryGetPlayer(harness.ConnectionOf(clients[0]), out _), Is.True);
        Assert.That(harness.Runtime.TryGetPlayer(harness.ConnectionOf(clients[2]), out _), Is.True);
    }

    /// <summary>每连接命令 Hint 独立累计，互不覆盖。</summary>
    [Test]
    public void CommandHints_DoNotCrossConnections()
    {
        using var harness = new DedicatedHarness(maxPlayers: 3);
        ClientSession[] clients = harness.JoinClients(2);
        NetConnectionId a = harness.ConnectionOf(clients[0]);
        NetConnectionId b = harness.ConnectionOf(clients[1]);

        harness.SendCommand(clients[0], frameHint: 4);
        harness.SendCommand(clients[1], frameHint: 9);
        harness.Runtime.Poll(1);

        Assert.That(harness.Runtime.TryGetAck(a, out long lastA, out long tickA), Is.True);
        Assert.That(harness.Runtime.TryGetAck(b, out long lastB, out long tickB), Is.True);
        Assert.That(lastA, Is.EqualTo(4));
        Assert.That(tickA, Is.EqualTo(4));
        Assert.That(lastB, Is.EqualTo(9));
        Assert.That(tickB, Is.EqualTo(9));

        harness.SendCommand(clients[0], frameHint: 5);
        harness.Runtime.Poll(2);
        harness.Runtime.TryGetAck(a, out lastA, out tickA);
        harness.Runtime.TryGetAck(b, out lastB, out tickB);

        Assert.That(lastA, Is.EqualTo(5));
        Assert.That(tickA, Is.EqualTo(5));
        Assert.That(lastB, Is.EqualTo(9));
        Assert.That(tickB, Is.EqualTo(0));
    }

    /// <summary>非 Owner PlayerId 的命令不得灌入该连接 ACK。</summary>
    [Test]
    public void CommandFromOtherPlayerId_IsIgnored()
    {
        using var harness = new DedicatedHarness(maxPlayers: 2);
        ClientSession client = harness.JoinClients(1)[0];
        NetConnectionId connection = harness.ConnectionOf(client);

        var actorId = new SimActorId(client.JoinAccept.EntityId.Value);
        InputFrame input = InputFrame.Empty(3, actorId);
        var command = new ClientCommand(3, senderPlayerId: 99, in input);
        client.SendApplication(
            (byte)RoomMessageKind.ClientCommand,
            NetChannel.CommandUnreliableRedundant,
            RoomCodec.WriteClientCommandBatch(new[] { command }));
        harness.Runtime.Poll(1);

        Assert.That(harness.Runtime.TryGetAck(connection, out long last, out long tick), Is.True);
        Assert.That(last, Is.EqualTo(0));
        Assert.That(tick, Is.EqualTo(0));
    }

    /// <summary>第二拍起每连接独立下发 ReplicationFrame，Sequence 不串线。</summary>
    [Test]
    public void Playing_SendsPerConnectionReplicationFrames()
    {
        using var harness = new DedicatedHarness(maxPlayers: 2, new FramingAuthorityWorld());
        ClientSession[] clients = harness.JoinClients(2);

        harness.Runtime.Poll(20);
        clients[0].Poll(20);
        clients[1].Poll(20);

        ReplicationFrame frameA = DequeueFrame(clients[0]);
        ReplicationFrame frameB = DequeueFrame(clients[1]);
        Assert.That(frameA.Sequence.Value, Is.EqualTo(0));
        Assert.That(frameB.Sequence.Value, Is.EqualTo(0));
        Assert.That(frameA.Tick, Is.EqualTo(frameB.Tick));

        harness.Runtime.Poll(40);
        clients[0].Poll(40);
        clients[1].Poll(40);
        Assert.That(DequeueFrame(clients[0]).Sequence.Value, Is.EqualTo(1));
        Assert.That(DequeueFrame(clients[1]).Sequence.Value, Is.EqualTo(1));
    }

    /// <summary>RequestMatchEnd 向仍在线连接下发 MatchEnd 并结束 Session。</summary>
    [Test]
    public void RequestMatchEnd_SendsMatchEndAndEndsClients()
    {
        using var harness = new DedicatedHarness(maxPlayers: 2);
        ClientSession[] clients = harness.JoinClients(2);

        harness.Runtime.RequestMatchEnd();
        harness.Runtime.Poll(1);
        clients[0].Poll(1);
        clients[1].Poll(1);

        Assert.That(TryDequeueMatchEnd(clients[0], out MatchEndMessage endA), Is.True);
        Assert.That(endA.Reason, Is.EqualTo(MatchEndReason.Completed));
        Assert.That(clients[0].State, Is.EqualTo(ClientSessionState.Ended));
        Assert.That(clients[1].State, Is.EqualTo(ClientSessionState.Ended));
        Assert.That(harness.Runtime.MatchPhase, Is.EqualTo(DedicatedMatchPhase.Lobby));
        Assert.That(harness.Runtime.JoinedPlayerCount, Is.EqualTo(0));
    }

    /// <summary>最后一名玩家离开后回到 Lobby，随后可再次 Join。</summary>
    [Test]
    public void LastDisconnect_ReturnsToLobbyAndAllowsRejoin()
    {
        using var harness = new DedicatedHarness(maxPlayers: 2);
        ClientSession first = harness.JoinClients(1)[0];
        NetConnectionId dropped = harness.ConnectionOf(first);

        harness.Runtime.Session.Disconnect(dropped, DisconnectReason.Requested);
        first.Poll(1);
        harness.Runtime.Poll(1);

        Assert.That(harness.Runtime.MatchPhase, Is.EqualTo(DedicatedMatchPhase.Lobby));
        Assert.That(harness.Runtime.JoinedPlayerCount, Is.EqualTo(0));

        ClientSession second = harness.JoinClients(1)[0];
        Assert.That(second.State, Is.EqualTo(ClientSessionState.Joined));
        Assert.That(harness.Runtime.MatchPhase, Is.EqualTo(DedicatedMatchPhase.Playing));
        Assert.That(harness.Runtime.ShouldExit, Is.False);
    }

    /// <summary>无人加入的 Lobby 到达超时后请求退出，退出码仍为 Success。</summary>
    [Test]
    public void EmptyLobbyTimeout_WithoutPlayers_RequestsExit()
    {
        ServerLaunchConfig launch = ServerLaunchConfig.CreateDefault(
            7777,
            contentVersion: 1,
            maxPlayers: 2,
            emptyLobbyTimeoutMs: 50);
        using var harness = new DedicatedHarness(launch);

        harness.Runtime.Poll(0);
        Assert.That(harness.Runtime.ShouldExit, Is.False);
        Assert.That(harness.Runtime.IsReady, Is.True);

        harness.Runtime.Poll(49);
        Assert.That(harness.Runtime.ShouldExit, Is.False);

        harness.Runtime.Poll(50);
        Assert.That(harness.Runtime.ShouldExit, Is.True);
        Assert.That(harness.Runtime.IsReady, Is.False);
        Assert.That(harness.Runtime.ExitCode, Is.EqualTo(ServerExitCode.Success));
    }

    /// <summary>曾经有人加入后，空房超时不再触发，默认配置仍可再入房。</summary>
    [Test]
    public void EmptyLobbyTimeout_AfterFirstJoin_DoesNotExit()
    {
        ServerLaunchConfig launch = ServerLaunchConfig.CreateDefault(
            7777,
            contentVersion: 1,
            maxPlayers: 2,
            emptyLobbyTimeoutMs: 50);
        using var harness = new DedicatedHarness(launch);
        ClientSession first = harness.JoinClients(1)[0];

        harness.Runtime.Session.Disconnect(harness.ConnectionOf(first), DisconnectReason.Requested);
        first.Poll(1);
        harness.Runtime.Poll(200);

        Assert.That(harness.Runtime.ShouldExit, Is.False);
        Assert.That(harness.Runtime.MatchPhase, Is.EqualTo(DedicatedMatchPhase.Lobby));

        ClientSession second = harness.JoinClients(1)[0];
        Assert.That(second.State, Is.EqualTo(ClientSessionState.Joined));
    }

    /// <summary>玩家构建策略下，最后一名离开后请求退出且不再 Accept。</summary>
    [Test]
    public void ExitOnMatchEnd_LastDisconnect_RequestsExitAndRejectsRejoin()
    {
        ServerLaunchConfig launch = ServerLaunchConfig.CreateDefault(
            7777,
            contentVersion: 1,
            maxPlayers: 2,
            exitOnMatchEnd: true);
        using var harness = new DedicatedHarness(launch);
        ClientSession first = harness.JoinClients(1)[0];

        harness.Runtime.Session.Disconnect(harness.ConnectionOf(first), DisconnectReason.Requested);
        first.Poll(1);
        harness.Runtime.Poll(1);

        Assert.That(harness.Runtime.ShouldExit, Is.True);
        Assert.That(harness.Runtime.MatchPhase, Is.EqualTo(DedicatedMatchPhase.Lobby));
        Assert.That(harness.Runtime.IsReady, Is.False);

        ClientSession rejected = harness.TryJoinOne();
        Assert.That(rejected.State, Is.Not.EqualTo(ClientSessionState.Joined));
    }

    /// <summary>RequestMatchEnd 在 ExitOnMatchEnd 时同样请求进程退出。</summary>
    [Test]
    public void ExitOnMatchEnd_RequestMatchEnd_RequestsExit()
    {
        ServerLaunchConfig launch = ServerLaunchConfig.CreateDefault(
            7777,
            contentVersion: 1,
            maxPlayers: 2,
            exitOnMatchEnd: true);
        using var harness = new DedicatedHarness(launch);
        harness.JoinClients(1);

        harness.Runtime.RequestMatchEnd();
        harness.Runtime.Poll(1);

        Assert.That(harness.Runtime.ShouldExit, Is.True);
        Assert.That(harness.Runtime.ExitCode, Is.EqualTo(ServerExitCode.Success));
    }

    /// <summary>JoinAccept 实体 Id 必须来自权威 World，而不是仅 Match 槽位占位。</summary>
    [Test]
    public void JoinAccept_UsesAuthorityEntityId()
    {
        using var harness = new DedicatedHarness(maxPlayers: 1, new StubAuthorityWorld(entityOffset: 40));
        ClientSession client = harness.JoinClients(1)[0];

        Assert.That(client.JoinAccept.EntityId.Value, Is.EqualTo(40));
        Assert.That(
            harness.Runtime.TryGetPlayer(harness.ConnectionOf(client), out DedicatedPlayerRuntime player),
            Is.True);
        Assert.That(player.EntityId.Value, Is.EqualTo(40));
    }

    static ReplicationFrame DequeueFrame(ClientSession client)
    {
        Assert.That(client.TryDequeueApplication(out SessionApplicationPacket packet), Is.True);
        Assert.That(packet.MessageType, Is.EqualTo((byte)RoomMessageKind.ReplicationFrame));
        return ReplicationFrameCodec.Decode(packet.Payload);
    }

    static bool TryDequeueMatchEnd(ClientSession client, out MatchEndMessage message)
    {
        message = default;
        while (client.TryDequeueApplication(out SessionApplicationPacket packet))
        {
            if (packet.MessageType != (byte)RoomMessageKind.MatchEnd)
                continue;
            message = RoomCodec.ReadMatchEnd(packet.Payload);
            return true;
        }

        return false;
    }

    /// <summary>Loopback Dedicated 与多个 ClientSession 的测试夹具。</summary>
    sealed class DedicatedHarness : IDisposable
    {
        readonly LoopbackNetwork _network = new();
        readonly List<ClientSession> _clients = new();
        readonly SessionConfig _clientConfig;
        readonly Dictionary<NetPlayerId, NetConnectionId> _connections = new();

        public DedicatedHarness(int maxPlayers, IDedicatedAuthorityWorld authority = null)
            : this(ServerLaunchConfig.CreateDefault(7777, contentVersion: 1, maxPlayers), authority)
        {
        }

        /// <summary>用指定启动配置创建 Loopback Dedicated。</summary>
        public DedicatedHarness(ServerLaunchConfig launch, IDedicatedAuthorityWorld authority = null)
        {
            Runtime = DedicatedServerRuntime.TryStart(
                new LoopbackTransport(_network),
                launch,
                authority ?? new StubAuthorityWorld(),
                out ServerExitCode exit);
            Assert.That(Runtime, Is.Not.Null);
            Assert.That(exit, Is.EqualTo(ServerExitCode.Success));
            _clientConfig = launch.CreateSessionConfig();
        }

        public DedicatedServerRuntime Runtime { get; }

        /// <summary>启动并接纳指定数量客户端。</summary>
        public ClientSession[] JoinClients(int count)
        {
            var created = new ClientSession[count];
            for (int i = 0; i < count; i++)
            {
                created[i] = new ClientSession(new LoopbackTransport(_network), _clientConfig);
                _clients.Add(created[i]);
                created[i].Start(Endpoint, 0);
            }

            Runtime.Poll(0);
            for (int i = 0; i < created.Length; i++)
                created[i].Poll(0);

            for (int i = 0; i < created.Length; i++)
            {
                Assert.That(created[i].State, Is.EqualTo(ClientSessionState.Joined));
                Assert.That(
                    Runtime.TryGetPlayerByPlayerId(
                        created[i].JoinAccept.PlayerId,
                        out DedicatedPlayerRuntime player),
                    Is.True);
                _connections[created[i].JoinAccept.PlayerId] = player.Slot.ConnectionId;
            }

            return created;
        }

        /// <summary>按已 Join 客户端查找连接。</summary>
        public NetConnectionId ConnectionOf(ClientSession client) =>
            _connections[client.JoinAccept.PlayerId];

        /// <summary>发起一次 Join 但不要求成功，用于验证退出后拒收。</summary>
        public ClientSession TryJoinOne()
        {
            var client = new ClientSession(new LoopbackTransport(_network), _clientConfig);
            _clients.Add(client);
            client.Start(Endpoint, 0);
            Runtime.Poll(0);
            client.Poll(0);
            return client;
        }

        /// <summary>发送一条只含 FrameHint 的命令批。</summary>
        public void SendCommand(ClientSession client, long frameHint)
        {
            var actorId = new SimActorId(client.JoinAccept.EntityId.Value);
            InputFrame input = InputFrame.Empty(frameHint, actorId);
            var command = new ClientCommand(frameHint, client.JoinAccept.PlayerId.Value, in input);
            byte[] body = RoomCodec.WriteClientCommandBatch(new[] { command });
            client.SendApplication(
                (byte)RoomMessageKind.ClientCommand,
                NetChannel.CommandUnreliableRedundant,
                body);
        }

        public void Dispose()
        {
            for (int i = 0; i < _clients.Count; i++)
                _clients[i].Dispose();
            Runtime?.Dispose();
        }
    }

    /// <summary>测试用权威世界：接受 Join 但不创建 Actor。</summary>
    sealed class StubAuthorityWorld : IDedicatedAuthorityWorld
    {
        readonly int _entityOffset;

        public StubAuthorityWorld(int entityOffset = 0)
        {
            _entityOffset = entityOffset;
        }

        public long CurrentFrame => -1;

        public bool TryAcceptPlayer(in MatchPlayerSlot slot, out NetEntityId entityId)
        {
            entityId = _entityOffset > 0
                ? new NetEntityId(_entityOffset)
                : slot.EntityId;
            return slot.ConnectionId.IsValid && entityId.IsValid;
        }

        public void ApplyCommands(NetConnectionId connectionId, ClientCommand[] commands)
        {
        }

        public void RemovePlayer(NetConnectionId connectionId)
        {
        }

        public void Advance(long nowMs)
        {
        }

        public int PeekAdvanceSteps(long nowMs) => 0;

        public float InterpolationAlpha => 0f;

        public void PublishImmediateReplication()
        {
        }

        public void DrainOutboundReplication(List<DedicatedReplicationSend> results)
        {
            results?.Clear();
        }

        public void Dispose()
        {
        }
    }

    /// <summary>第二拍起为每个已接纳连接编一帧空 ReplicationFrame。</summary>
    sealed class FramingAuthorityWorld : IDedicatedAuthorityWorld
    {
        readonly Dictionary<NetConnectionId, ReplicationServer> _servers = new();
        readonly List<DedicatedReplicationSend> _queued = new();
        readonly byte[] _emptyApplication =
        {
            1,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0
        };
        bool _hasClock;
        long _frame = -1;

        public long CurrentFrame => _frame;

        public bool TryAcceptPlayer(in MatchPlayerSlot slot, out NetEntityId entityId)
        {
            entityId = slot.EntityId;
            if (!slot.ConnectionId.IsValid || !entityId.IsValid)
                return false;
            _servers[slot.ConnectionId] = new ReplicationServer();
            return true;
        }

        public void ApplyCommands(NetConnectionId connectionId, ClientCommand[] commands)
        {
        }

        public void RemovePlayer(NetConnectionId connectionId) => _servers.Remove(connectionId);

        public int PeekAdvanceSteps(long nowMs) => _hasClock ? 1 : 0;

        public float InterpolationAlpha => 0f;

        public void Advance(long nowMs)
        {
            if (!_hasClock)
            {
                _hasClock = true;
                return;
            }

            _frame = _frame < 0 ? 0 : _frame + 1;
            _queued.Clear();
            foreach (KeyValuePair<NetConnectionId, ReplicationServer> pair in _servers)
            {
                ReplicationFrame frame = pair.Value.BuildFrame(
                    new NetTick(_frame),
                    Array.Empty<ReplicationEntityState>(),
                    _emptyApplication);
                _queued.Add(new DedicatedReplicationSend(
                    pair.Key,
                    ReplicationFrameCodec.Encode(frame)));
            }
        }

        public void PublishImmediateReplication()
        {
        }

        public void DrainOutboundReplication(List<DedicatedReplicationSend> results)
        {
            results.Clear();
            for (int i = 0; i < _queued.Count; i++)
                results.Add(_queued[i]);
            _queued.Clear();
        }

        public void Dispose()
        {
            _servers.Clear();
            _queued.Clear();
        }
    }

    /// <summary>StartServer 立即失败，模拟端口占用。</summary>
    sealed class ThrowingBindTransport : INetTransport
    {
        public bool IsRunning => false;
        public bool IsServer => false;
        public NetEndpoint? LocalEndpoint => null;
        public IReadOnlyList<NetConnectionId> Connections => Array.Empty<NetConnectionId>();
        public NetMetricsSnapshot Metrics => default;

        public void StartServer(NetEndpoint endpoint) =>
            throw new InvalidOperationException("bind failed");

        public void StartClient(NetEndpoint endpoint) =>
            throw new InvalidOperationException("not supported");

        public void Poll()
        {
        }

        public void Send(NetConnectionId connectionId, NetChannel channel, byte[] payload)
        {
        }

        public bool TryReceive(out NetPacket packet)
        {
            packet = default;
            return false;
        }

        public void Disconnect(NetConnectionId connectionId, DisconnectReason reason)
        {
        }

        public void Dispose()
        {
        }
    }
}

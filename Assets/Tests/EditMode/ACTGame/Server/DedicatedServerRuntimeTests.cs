using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>W5 Dedicated Runtime：无本地玩家、N 连接身份、断开隔离与每连接 ACK。</summary>
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
        Assert.That(harness.Runtime.ExitCode, Is.EqualTo(ServerExitCode.Success));
        Assert.That(client.State, Is.EqualTo(ClientSessionState.Joined));
        Assert.That(client.JoinAccept.AuthorityEntityId.IsValid, Is.False);
        Assert.That(client.JoinAccept.EntityId.IsValid, Is.True);
        Assert.That(harness.Runtime.JoinedPlayerCount, Is.EqualTo(1));
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

    /// <summary>Loopback Dedicated 与多个 ClientSession 的测试夹具。</summary>
    sealed class DedicatedHarness : IDisposable
    {
        readonly LoopbackNetwork _network = new();
        readonly List<ClientSession> _clients = new();
        readonly SessionConfig _clientConfig;
        readonly Dictionary<NetPlayerId, NetConnectionId> _connections = new();

        public DedicatedHarness(int maxPlayers)
        {
            ServerLaunchConfig launch = ServerLaunchConfig.CreateDefault(7777, contentVersion: 1, maxPlayers);
            Runtime = DedicatedServerRuntime.TryStart(
                new LoopbackTransport(_network),
                launch,
                new StubAuthorityWorld(),
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
        public long CurrentFrame => -1;

        public bool TryAcceptPlayer(in MatchPlayerSlot slot) => slot.ConnectionId.IsValid;

        public void ApplyCommands(NetConnectionId connectionId, ClientCommand[] commands)
        {
        }

        public void RemovePlayer(NetConnectionId connectionId)
        {
        }

        public void Advance(long nowMs)
        {
        }

        public void Dispose()
        {
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

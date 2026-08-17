using System.Collections.Generic;
using NUnit.Framework;

/// <summary>用纯 C# Loopback FakeGame 验证多连接 Session 的 Join、隔离、心跳与 Kick。</summary>
public sealed class SessionIntegrationTests
{
    static readonly NetEndpoint Endpoint = new("session-loopback", 7777);

    /// <summary>三个客户端应获得互异 ConnectionId、PlayerId，并保持各自 JoinAccept。</summary>
    [Test]
    public void ThreeClients_JoinWithDistinctConnectionAndPlayerIds()
    {
        using var harness = new SessionHarness(maxRemotePlayers: 3);
        ClientSession[] clients = harness.CreateAndJoinClients(3);

        Assert.That(harness.Server.ConnectionCount, Is.EqualTo(3));
        var playerIds = new HashSet<NetPlayerId>();
        var entityIds = new HashSet<NetEntityId>();
        for (int i = 0; i < clients.Length; i++)
        {
            Assert.That(clients[i].State, Is.EqualTo(ClientSessionState.Joined));
            playerIds.Add(clients[i].JoinAccept.PlayerId);
            entityIds.Add(clients[i].JoinAccept.EntityId);
        }

        Assert.That(playerIds, Has.Count.EqualTo(3));
        Assert.That(entityIds, Has.Count.EqualTo(3));
    }

    /// <summary>服务端断开一个客户端后，其余连接仍可独立发送应用消息。</summary>
    [Test]
    public void DisconnectOneOfThree_OthersRemainConnected()
    {
        using var harness = new SessionHarness(maxRemotePlayers: 3);
        ClientSession[] clients = harness.CreateAndJoinClients(3);
        NetConnectionId disconnected = harness.ConnectionByPlayer[clients[1].JoinAccept.PlayerId];

        harness.Server.Disconnect(disconnected, DisconnectReason.ServerShutdown);
        clients[1].Poll(1);
        clients[0].SendApplication(5, NetChannel.CommandUnreliableRedundant, new byte[] { 10 });
        clients[2].SendApplication(5, NetChannel.CommandUnreliableRedundant, new byte[] { 30 });
        harness.Server.Poll(1);

        Assert.That(clients[1].State, Is.EqualTo(ClientSessionState.Ended));
        Assert.That(clients[0].State, Is.EqualTo(ClientSessionState.Joined));
        Assert.That(clients[2].State, Is.EqualTo(ClientSessionState.Joined));
        Assert.That(harness.Server.ConnectionCount, Is.EqualTo(2));
        Assert.That(DrainApplicationCount(harness.Server), Is.EqualTo(2));
    }

    /// <summary>停止上行的连接单独超时，活跃连接不会被连带 Kick。</summary>
    [Test]
    public void IdleClient_IsKickedWithoutAffectingActiveClients()
    {
        using var harness = new SessionHarness(maxRemotePlayers: 3);
        ClientSession[] clients = harness.CreateAndJoinClients(3);
        clients[0].SendApplication(5, NetChannel.CommandUnreliableRedundant, new byte[] { 1 });
        clients[2].SendApplication(5, NetChannel.CommandUnreliableRedundant, new byte[] { 3 });

        harness.Server.Poll(10000);
        clients[1].Poll(10000);

        Assert.That(clients[1].State, Is.EqualTo(ClientSessionState.Ended));
        Assert.That(clients[1].LastDisconnectReason, Is.EqualTo(DisconnectReason.Timeout));
        Assert.That(clients[0].State, Is.EqualTo(ClientSessionState.Joined));
        Assert.That(clients[2].State, Is.EqualTo(ClientSessionState.Joined));
        Assert.That(harness.Server.ConnectionCount, Is.EqualTo(2));
    }

    /// <summary>客户端自动心跳应被服务端回显，并使用注入 Poll 时刻计算 RTT。</summary>
    [Test]
    public void Heartbeat_EchoComputesRtt()
    {
        using var harness = new SessionHarness(maxRemotePlayers: 1);
        ClientSession client = harness.CreateAndJoinClients(1)[0];

        client.Poll(500);
        harness.Server.Poll(500);
        client.Poll(550);

        Assert.That(client.RttMs, Is.EqualTo(50));
        Assert.That(client.State, Is.EqualTo(ClientSessionState.Joined));
    }

    /// <summary>已满 Session 必须拒绝额外客户端且不影响既有连接。</summary>
    [Test]
    public void JoinBeyondCapacity_IsRejectedAsServerFull()
    {
        using var harness = new SessionHarness(maxRemotePlayers: 1);
        ClientSession accepted = harness.CreateAndJoinClients(1)[0];
        ClientSession rejected = harness.CreateClient();
        rejected.Start(Endpoint, 0);
        harness.Server.Poll(0);
        rejected.Poll(0);

        Assert.That(accepted.State, Is.EqualTo(ClientSessionState.Joined));
        Assert.That(rejected.State, Is.EqualTo(ClientSessionState.Ended));
        Assert.That(rejected.LastDisconnectReason, Is.EqualTo(DisconnectReason.ServerFull));
        Assert.That(harness.Server.ConnectionCount, Is.EqualTo(1));
    }

    /// <summary>清空服务端应用队列并返回条数。</summary>
    static int DrainApplicationCount(ServerSession server)
    {
        int count = 0;
        while (server.TryDequeueApplication(out _))
            count++;
        return count;
    }

    /// <summary>管理一个服务端、多个客户端和 FakeGame 实体接纳。</summary>
    sealed class SessionHarness : System.IDisposable
    {
        readonly LoopbackNetwork _network = new();
        readonly List<ClientSession> _clients = new();
        readonly SessionConfig _config;

        public SessionHarness(int maxRemotePlayers)
        {
            _config = new SessionConfig(
                new NetworkProtocolVersion(1),
                contentVersion: 7,
                maxRemotePlayers: maxRemotePlayers,
                idleTimeoutMs: 10000,
                heartbeatIntervalMs: 500);
            Server = new ServerSession(
                new LoopbackTransport(_network),
                _config,
                Endpoint);
        }

        public ServerSession Server { get; }

        public Dictionary<NetPlayerId, NetConnectionId> ConnectionByPlayer { get; } = new();

        /// <summary>创建并登记一个尚未启动的 Loopback 客户端 Session。</summary>
        public ClientSession CreateClient()
        {
            var client = new ClientSession(new LoopbackTransport(_network), _config);
            _clients.Add(client);
            return client;
        }

        /// <summary>批量 Join，并由 FakeGame 为每个 Player 分配唯一实体。</summary>
        public ClientSession[] CreateAndJoinClients(int count)
        {
            var created = new ClientSession[count];
            for (int i = 0; i < count; i++)
            {
                created[i] = CreateClient();
                created[i].Start(Endpoint, 0);
            }

            Server.Poll(0);
            while (Server.TryDequeuePlayerRequest(out SessionPlayerRequest request))
            {
                ConnectionByPlayer.Add(request.PlayerId, request.ConnectionId);
                Server.AcceptPlayer(
                    request.ConnectionId,
                    new NetEntityId(100 + request.PlayerId.Value),
                    new NetEntityId(1),
                    new NetTick(0));
            }

            for (int i = 0; i < created.Length; i++)
                created[i].Poll(0);
            return created;
        }

        /// <summary>按客户端先、服务端后的顺序释放所有 Loopback 端点。</summary>
        public void Dispose()
        {
            for (int i = 0; i < _clients.Count; i++)
                _clients[i].Dispose();
            Server.Dispose();
        }
    }
}

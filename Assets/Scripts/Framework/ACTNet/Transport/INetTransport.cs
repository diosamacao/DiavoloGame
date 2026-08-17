using System;
using System.Collections.Generic;

/// <summary>与客户端/权威方向无关的多连接字节传输契约。</summary>
public interface INetTransport : IDisposable
{
    /// <summary>当前是否已启动并可 Poll。</summary>
    bool IsRunning { get; }

    /// <summary>当前实例是否以服务端角色启动。</summary>
    bool IsServer { get; }

    /// <summary>实际本地绑定端点；未启动时为 null。</summary>
    NetEndpoint? LocalEndpoint { get; }

    /// <summary>当前本地作用域内的连接快照。</summary>
    IReadOnlyList<NetConnectionId> Connections { get; }

    /// <summary>最近一次可观测网络指标。</summary>
    NetMetricsSnapshot Metrics { get; }

    /// <summary>以服务端角色监听端点；端口 0 由实现分配。</summary>
    void StartServer(NetEndpoint endpoint);

    /// <summary>以客户端角色连接远端；成功启动后 Connections 含服务端连接。</summary>
    void StartClient(NetEndpoint endpoint);

    /// <summary>轮询底层传输并把到达数据报放入接收队列。</summary>
    void Poll();

    /// <summary>向指定连接按声明通道语义发送独立载荷。</summary>
    void Send(NetConnectionId connectionId, NetChannel channel, byte[] payload);

    /// <summary>取出一条已到达的数据包。</summary>
    bool TryReceive(out NetPacket packet);

    /// <summary>关闭指定本地连接；原因供日志和后续可靠实现使用。</summary>
    void Disconnect(NetConnectionId connectionId, DisconnectReason reason);
}

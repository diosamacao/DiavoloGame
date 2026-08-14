/// <summary>
/// 复制传输：只收发字节，不跑模拟。Host / Dedicated / UDP 只换实现。
/// </summary>
public interface IReplicationTransport
{
    /// <summary>客户端把命令字节发给权威。</summary>
    void SendClientToAuthority(byte[] payload);

    /// <summary>权威把 Tick 字节发给所有客户端。</summary>
    void SendAuthorityToClients(byte[] payload);

    /// <summary>推进传输时钟并投递到期包；延迟 0 时 Send 后调用即可取出。</summary>
    void Pump();

    /// <summary>权威侧取出一条已到期的上行载荷。</summary>
    bool TryDequeueAuthority(out byte[] payload);

    /// <summary>客户端侧取出一条已到期的下行载荷（NS1 单客户端）。</summary>
    bool TryDequeueClient(out byte[] payload);
}

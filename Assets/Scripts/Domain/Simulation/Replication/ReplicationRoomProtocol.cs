/// <summary>NS5 最小房间常量；与 Snapshot 编解码版本独立。</summary>
public static class ReplicationRoomProtocol
{
    /// <summary>房间信封协议版本。</summary>
    public const byte RoomCodecVersion = 1;

    /// <summary>Join 握手使用的逻辑协议号；不匹配则拒收。</summary>
    public const int ProtocolVersion = 1;

    /// <summary>首版房间人数上限（Host + 一名客机）。</summary>
    public const int MaxPlayers = 2;

    /// <summary>无包超过该毫秒则剔除或结束房间。</summary>
    public const int IdleTimeoutMs = 10000;

    /// <summary>迟到输入仍写入下一权威帧的最大落后逻辑帧数。</summary>
    public const int LateInputWindowFrames = 8;

    /// <summary>每包重发最近几条 ClientCommand，降低 UDP 丢单帧 Attack 边沿的概率。</summary>
    public const int InputRedundancyCount = 3;

    /// <summary>默认 UDP 监听端口。</summary>
    public const int DefaultPort = 7777;

    /// <summary>客机心跳间隔。</summary>
    public const int HeartbeatIntervalMs = 500;
}

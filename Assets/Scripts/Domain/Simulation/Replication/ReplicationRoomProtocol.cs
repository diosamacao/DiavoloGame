/// <summary>ACT 复制应用选择的 Session 默认值与输入冗余策略。</summary>
public static class ReplicationRoomProtocol
{
    /// <summary>传给 SessionConfig 的线协议号。</summary>
    public const int ProtocolVersion = 1;

    /// <summary>传给 SessionConfig 的默认空闲超时。</summary>
    public const int IdleTimeoutMs = 10000;

    /// <summary>迟到输入仍写入下一权威帧的最大落后逻辑帧数。</summary>
    public const int LateInputWindowFrames = 8;

    /// <summary>每包重发最近几条 ClientCommand，降低 UDP 丢单帧 Attack 边沿的概率。</summary>
    public const int InputRedundancyCount = 3;

    /// <summary>默认 UDP 监听端口。</summary>
    public const int DefaultPort = 7777;

    /// <summary>传给 SessionConfig 的默认客机心跳间隔。</summary>
    public const int HeartbeatIntervalMs = 500;
}

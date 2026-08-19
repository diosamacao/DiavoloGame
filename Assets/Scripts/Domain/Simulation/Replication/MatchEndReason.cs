/// <summary>Dedicated MatchEnd 原因；W7 临时应用消息，W10 再迁可靠事件通道。</summary>
public enum MatchEndReason : byte
{
    /// <summary>Playing 后房间清空。</summary>
    EmptyRoom = 1,

    /// <summary>对局被判定完成或被请求结束。</summary>
    Completed = 2,

    /// <summary>服务器停服或运行时释放。</summary>
    ServerShutdown = 3,
}

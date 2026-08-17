/// <summary>ACT 复制层占用的 Session 应用消息类型。</summary>
public enum RoomMessageKind : byte
{
    /// <summary>正文为 ReplicationCodec 上行命令。</summary>
    ClientCommand = 5,

    /// <summary>正文为 appliedFrameHint + AuthorityTick 字节。</summary>
    AuthorityTick = 6,
}

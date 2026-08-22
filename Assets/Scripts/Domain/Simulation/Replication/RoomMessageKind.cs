/// <summary>ACT 复制层占用的 Session 应用消息类型。</summary>
public enum RoomMessageKind : byte
{
    /// <summary>正文为 ReplicationCodec 上行命令。</summary>
    ClientCommand = 5,

    /// <summary>正文为 ACTNet.Replication Version 1 ReplicationFrame 字节。</summary>
    ReplicationFrame = 6,

    /// <summary>正文为 MatchEnd；占用 8，避开 Session Kick=7。</summary>
    MatchEnd = 8,

    /// <summary>正文为可靠命中事件包；与 Snapshot 分轨，禁止再塞进帧内冗余。</summary>
    ReplicationEvent = 9,

    /// <summary>客机请求全量 Spawn 恢复；正文可为空。</summary>
    ReplicationRecover = 10,
}

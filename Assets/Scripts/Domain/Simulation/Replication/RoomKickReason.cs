/// <summary>房间踢出原因。</summary>
public enum RoomKickReason : byte
{
    /// <summary>超过空闲超时无包。</summary>
    IdleTimeout = 1,

    /// <summary>权威进程结束或主动关房。</summary>
    HostEnded = 2,
}

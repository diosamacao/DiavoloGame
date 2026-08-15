/// <summary>权威踢出通知。</summary>
public readonly struct RoomKick
{
    /// <summary>创建踢出通知。</summary>
    public RoomKick(RoomKickReason reason) => Reason = reason;

    /// <summary>踢出原因。</summary>
    public RoomKickReason Reason { get; }
}

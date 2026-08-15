/// <summary>入房拒绝。</summary>
public readonly struct RoomJoinReject
{
    /// <summary>创建拒绝回包。</summary>
    public RoomJoinReject(RoomRejectReason reason) => Reason = reason;

    /// <summary>拒绝原因。</summary>
    public RoomRejectReason Reason { get; }
}

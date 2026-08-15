/// <summary>入房被拒原因。</summary>
public enum RoomRejectReason : byte
{
    /// <summary>内容或房间协议版本不一致。</summary>
    VersionMismatch = 1,

    /// <summary>已有第二名玩家。</summary>
    RoomFull = 2,
}

/// <summary>客机入房请求：只校验版本，不含玩法状态。</summary>
public readonly struct RoomJoinRequest
{
    /// <summary>创建入房请求。</summary>
    public RoomJoinRequest(int contentVersion, int protocolVersion)
    {
        ContentVersion = contentVersion;
        ProtocolVersion = protocolVersion;
    }

    /// <summary>关卡/内容版本；双方必须一致。</summary>
    public int ContentVersion { get; }

    /// <summary>房间握手协议号。</summary>
    public int ProtocolVersion { get; }
}

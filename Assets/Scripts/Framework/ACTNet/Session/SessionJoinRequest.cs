using System;

/// <summary>客户端建立 Session 时提交的协议与内容版本。</summary>
public readonly struct SessionJoinRequest
{
    /// <summary>创建版本握手请求。</summary>
    public SessionJoinRequest(
        int contentVersion,
        NetworkProtocolVersion protocolVersion,
        ContentFingerprint gameplayFingerprint = default)
    {
        ContentVersion = contentVersion;
        ProtocolVersion = protocolVersion;
        GameplayFingerprint = gameplayFingerprint;
    }

    /// <summary>应用层内容版本。</summary>
    public int ContentVersion { get; }

    /// <summary>网络协议版本。</summary>
    public NetworkProtocolVersion ProtocolVersion { get; }

    /// <summary>Gameplay 内容指纹；Invalid 表示调用方未计算。</summary>
    public ContentFingerprint GameplayFingerprint { get; }
}

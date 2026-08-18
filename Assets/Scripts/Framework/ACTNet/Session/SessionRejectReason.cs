/// <summary>保持既有一字节线格式的 Session 建立拒绝原因。</summary>
public enum SessionRejectReason : byte
{
    /// <summary>协议号或内容版本不一致。</summary>
    VersionMismatch = 1,

    /// <summary>Session 已达到远端玩家容量。</summary>
    ServerFull = 2,

    /// <summary>Gameplay 无法为连接创建玩家实体。</summary>
    GameRejected = 3,

    /// <summary>Gameplay ContentFingerprint 不一致。</summary>
    ContentMismatch = 4,
}

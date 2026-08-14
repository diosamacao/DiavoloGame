using System;

/// <summary>客户端上行命令：只有量化输入，不含 HP / 命中 / 世界坐标 / 招式名。</summary>
public readonly struct ClientCommand : IEquatable<ClientCommand>
{
    /// <summary>创建一条上行命令。</summary>
    public ClientCommand(long frameHint, int senderPlayerId, in InputFrame input)
    {
        FrameHint = frameHint;
        SenderPlayerId = senderPlayerId;
        Input = input;
    }

    /// <summary>客户端认为对应的逻辑帧提示；权威可按到达窗口对齐。</summary>
    public long FrameHint { get; }

    /// <summary>发送方房间内玩家编号，不是 SimActorId。</summary>
    public int SenderPlayerId { get; }

    /// <summary>该玩家当帧量化输入。</summary>
    public InputFrame Input { get; }

    /// <summary>比较提示帧、发送方与输入字段。</summary>
    public bool Equals(ClientCommand other) =>
        FrameHint == other.FrameHint
        && SenderPlayerId == other.SenderPlayerId
        && Input.Equals(other.Input);

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is ClientCommand other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = FrameHint.GetHashCode();
            hash = (hash * 397) ^ SenderPlayerId;
            hash = (hash * 397) ^ Input.GetHashCode();
            return hash;
        }
    }
}

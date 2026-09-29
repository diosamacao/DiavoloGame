using System;

/// <summary>标识一次准备结果中的单包提交；只能交回创建它的服务端。</summary>
public readonly struct ReplicationPacketCommitToken : IEquatable<ReplicationPacketCommitToken>
{
    readonly ReplicationCommitTicket _ticket;

    /// <summary>创建只携带身份的外部令牌；不能提交到任何服务端。</summary>
    public ReplicationPacketCommitToken(Guid value)
    {
        Value = value == Guid.Empty ? throw new ArgumentException("Token 不能为空。", nameof(value)) : value;
        _ticket = null;
    }

    /// <summary>由准备结果绑定服务端与提交动作；票据不登记回服务端，保证 Prepare 纯读。</summary>
    internal ReplicationPacketCommitToken(Guid value, ReplicationCommitTicket ticket)
    {
        Value = value == Guid.Empty ? throw new ArgumentException("Token 不能为空。", nameof(value)) : value;
        _ticket = ticket ?? throw new ArgumentNullException(nameof(ticket));
    }

    /// <summary>令牌唯一值。</summary>
    public Guid Value { get; }
    internal ReplicationCommitTicket Ticket => _ticket;
    /// <inheritdoc />
    public bool Equals(ReplicationPacketCommitToken other) => Value.Equals(other.Value);
    /// <inheritdoc />
    public override bool Equals(object obj) => obj is ReplicationPacketCommitToken other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();
}

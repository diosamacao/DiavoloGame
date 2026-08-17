using System;

/// <summary>无异常控制流的网络操作结果；失败时携带稳定原因和可选诊断文本。</summary>
public readonly struct NetResult : IEquatable<NetResult>
{
    /// <summary>成功结果。</summary>
    public static NetResult Success => new(true, DisconnectReason.None, string.Empty);

    /// <summary>操作是否成功。</summary>
    public bool IsSuccess { get; }

    /// <summary>失败原因；成功时固定为 None。</summary>
    public DisconnectReason Reason { get; }

    /// <summary>仅用于日志和调试的诊断文本，不作为协议或分支依据。</summary>
    public string Message { get; }

    NetResult(bool isSuccess, DisconnectReason reason, string message)
    {
        IsSuccess = isSuccess;
        Reason = reason;
        Message = message ?? string.Empty;
    }

    /// <summary>创建带稳定断开原因的失败结果。</summary>
    public static NetResult Failure(DisconnectReason reason, string message = "")
    {
        if (reason == DisconnectReason.None)
            throw new ArgumentException("失败结果必须提供非 None 原因。", nameof(reason));
        return new NetResult(false, reason, message);
    }

    /// <inheritdoc />
    public bool Equals(NetResult other) =>
        IsSuccess == other.IsSuccess
        && Reason == other.Reason
        && string.Equals(Message, other.Message, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is NetResult other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = IsSuccess ? 1 : 0;
            hash = (hash * 397) ^ (int)Reason;
            hash = (hash * 397) ^ (Message != null ? Message.GetHashCode() : 0);
            return hash;
        }
    }

    /// <inheritdoc />
    public override string ToString() =>
        IsSuccess ? "Success" : $"{Reason}: {Message}";
}

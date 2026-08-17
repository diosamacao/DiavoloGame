using System;

/// <summary>与具体 Socket 类型解耦的网络主机与端口值对象。</summary>
public readonly struct NetEndpoint : IEquatable<NetEndpoint>
{
    /// <summary>主机名或 IP 文本；服务端可使用 0.0.0.0。</summary>
    public string Host { get; }

    /// <summary>端口；服务端允许 0 让系统分配临时端口。</summary>
    public int Port { get; }

    /// <summary>创建经过端口范围验证的网络端点。</summary>
    public NetEndpoint(string host, int port, bool allowEphemeralPort = false)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("网络主机不能为空。", nameof(host));
        int minimum = allowEphemeralPort ? 0 : 1;
        if (port < minimum || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));

        Host = host;
        Port = port;
    }

    /// <inheritdoc />
    public bool Equals(NetEndpoint other) =>
        Port == other.Port
        && string.Equals(Host, other.Host, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is NetEndpoint other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return ((Host != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Host) : 0) * 397)
                ^ Port;
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"{Host}:{Port}";
}

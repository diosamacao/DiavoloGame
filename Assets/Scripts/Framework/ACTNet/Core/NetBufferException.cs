using System;

/// <summary>网络载荷越界、长度非法或编码损坏时抛出的协议操作异常。</summary>
public sealed class NetBufferException : InvalidOperationException
{
    /// <summary>创建带诊断信息的网络 Buffer 异常。</summary>
    public NetBufferException(string message)
        : base(message)
    {
    }

    /// <summary>创建保留底层编码异常的网络 Buffer 异常。</summary>
    public NetBufferException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

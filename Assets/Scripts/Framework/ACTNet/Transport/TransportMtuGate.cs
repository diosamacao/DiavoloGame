using System;

/// <summary>单数据报 MTU 门禁；超限拒绝发送，由调用方拆包或记日志。</summary>
public static class TransportMtuGate
{
    /// <summary>局域网 Demo 默认 UDP 数据报上限（含通道头）。</summary>
    public const int DefaultMaxDatagramBytes = 1400;

    /// <summary>通道头：version + channel + kind + seq + ack + payloadLen。</summary>
    public const int HeaderBytes = 9;

    /// <summary>给定数据报上限时允许的应用正文长度。</summary>
    public static int MaxPayloadBytes(int maxDatagramBytes)
    {
        int max = maxDatagramBytes < HeaderBytes + 1 ? HeaderBytes + 1 : maxDatagramBytes;
        return max - HeaderBytes;
    }

    /// <summary>检查整包是否不超过配置 MTU。</summary>
    public static bool TryAccept(int datagramBytes, int maxDatagramBytes, out string reason)
    {
        if (maxDatagramBytes < HeaderBytes + 1)
        {
            reason = $"MTU {maxDatagramBytes} 小于最小通道头 {HeaderBytes + 1}。";
            return false;
        }

        if (datagramBytes < 0)
        {
            reason = "数据报长度不能为负。";
            return false;
        }

        if (datagramBytes > maxDatagramBytes)
        {
            reason = $"数据报 {datagramBytes} 超过配置 MTU {maxDatagramBytes}。";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>超限时抛出，供发送路径统一失败。</summary>
    public static void EnsureAccepted(int datagramBytes, int maxDatagramBytes)
    {
        if (TryAccept(datagramBytes, maxDatagramBytes, out string reason))
            return;
        throw new InvalidOperationException(reason);
    }
}

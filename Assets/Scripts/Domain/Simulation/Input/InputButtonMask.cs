using System;

/// <summary>集中定义 InputButton 到 bitset 的稳定映射。</summary>
public static class InputButtonMask
{
    /// <summary>返回按钮对应的单一 bit。</summary>
    public static ulong Of(InputButton button)
    {
        int bit = (int)button;
        if (bit < 0 || bit >= 64)
            throw new ArgumentOutOfRangeException(nameof(button));

        return 1ul << bit;
    }
}

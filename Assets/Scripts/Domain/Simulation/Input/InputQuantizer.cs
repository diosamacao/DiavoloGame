using System;

/// <summary>把设备浮点输入转换为固定精度整数，避免输入帧携带裸 float。</summary>
public static class InputQuantizer
{
    public const int AxisScale = 127;
    public const int YawScale = 10;

    /// <summary>把单轴钳制并量化到 [-127, 127]。</summary>
    public static sbyte QuantizeAxis(float value)
    {
        double clamped = Math.Max(-1d, Math.Min(1d, value));
        return (sbyte)Math.Round(clamped * AxisScale, MidpointRounding.AwayFromZero);
    }

    /// <summary>把量化轴还原为玩法侧使用的 [-1, 1] 浮点值；该值不作为传输权威。</summary>
    public static float DequantizeAxis(sbyte value) => value / (float)AxisScale;

    /// <summary>把偏航包裹到 [0, 360) 后按 0.1 度量化。</summary>
    public static ushort QuantizeYaw(float degrees)
    {
        double wrapped = degrees % 360d;
        if (wrapped < 0d)
            wrapped += 360d;

        int quantized = (int)Math.Round(wrapped * YawScale, MidpointRounding.AwayFromZero);
        return (ushort)(quantized >= 360 * YawScale ? 0 : quantized);
    }

    /// <summary>把 [0,3599] 的 0.1 度偏航还原为角度。</summary>
    public static float DequantizeYaw(ushort quantized) =>
        (quantized % (360 * YawScale)) / (float)YawScale;
}

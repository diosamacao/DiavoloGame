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

    /// <summary>把偏航包裹到 [-180, 180) 后按 0.1 度量化。</summary>
    public static short QuantizeYaw(float degrees)
    {
        double wrapped = degrees % 360d;
        if (wrapped >= 180d)
            wrapped -= 360d;
        else if (wrapped < -180d)
            wrapped += 360d;

        return (short)Math.Round(wrapped * YawScale, MidpointRounding.AwayFromZero);
    }
}

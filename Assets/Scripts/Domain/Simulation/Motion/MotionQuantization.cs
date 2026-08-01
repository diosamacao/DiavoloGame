using System;

/// <summary>浮点米/度 ↔ scaled-int 毫米/毫度的确定性量化。</summary>
public static class MotionQuantization
{
    public const int MmPerMeter = 1000;
    public const int MilliDegPerDeg = 1000;

    /// <summary>米 → 毫米（四舍五入到最近整数）。</summary>
    public static int MetersToMm(float meters) =>
        (int)Math.Round(meters * MmPerMeter, MidpointRounding.AwayFromZero);

    /// <summary>毫米 → 米。</summary>
    public static float MmToMeters(int mm) => mm / (float)MmPerMeter;

    /// <summary>度 → 毫度（四舍五入）。</summary>
    public static int DegreesToMilliDeg(float degrees) =>
        (int)Math.Round(degrees * MilliDegPerDeg, MidpointRounding.AwayFromZero);

    /// <summary>毫度 → 度。</summary>
    public static float MilliDegToDegrees(int milliDeg) => milliDeg / (float)MilliDegPerDeg;

    /// <summary>将偏航差规范到 (-180, 180] 度后再量化为毫度。</summary>
    public static int WrapDegreesToMilliDeg(float degrees)
    {
        float wrapped = degrees;
        while (wrapped <= -180f)
            wrapped += 360f;
        while (wrapped > 180f)
            wrapped -= 360f;
        return DegreesToMilliDeg(wrapped);
    }
}

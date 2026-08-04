using System;

/// <summary>水平轴对齐盒障碍（毫米）；用于静态硬挡烘焙与 ResolveMove。</summary>
[Serializable]
public struct SimStaticAabb
{
    public int MinXMm;
    public int MaxXMm;
    public int MinZMm;
    public int MaxZMm;

    /// <summary>规范化 min/max 后构造。</summary>
    public SimStaticAabb(int minXMm, int maxXMm, int minZMm, int maxZMm)
    {
        if (minXMm <= maxXMm)
        {
            MinXMm = minXMm;
            MaxXMm = maxXMm;
        }
        else
        {
            MinXMm = maxXMm;
            MaxXMm = minXMm;
        }

        if (minZMm <= maxZMm)
        {
            MinZMm = minZMm;
            MaxZMm = maxZMm;
        }
        else
        {
            MinZMm = maxZMm;
            MaxZMm = minZMm;
        }
    }

    /// <summary>按半径膨胀后的水平盒，供圆盘中心点检测。</summary>
    public SimStaticAabb Expanded(int radiusMm)
    {
        int r = Math.Max(0, radiusMm);
        return new SimStaticAabb(MinXMm - r, MaxXMm + r, MinZMm - r, MaxZMm + r);
    }
}

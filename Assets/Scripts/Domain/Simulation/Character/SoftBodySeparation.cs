using System;

/// <summary>
/// 角色水平圆盘软弹开：按重叠量与质量比推开；不可推动体像墙，推力全给对方。
/// </summary>
public static class SoftBodySeparation
{
    public const int FactorMilliMax = 1000;

    /// <summary>质量哨兵：≤0 视为不可推动（本帧推量为 0）。</summary>
    public const int ImmovableMass = 0;

    /// <summary>
    /// 就地修正 positions[0..count)；调用方须已按稳定 Id 升序填入。
    /// masses：相对质量，越大越难被推；ImmovableMass 表示坚挺不动。
    /// </summary>
    public static void Resolve(
        SimVec2[] positions,
        int[] radiiMm,
        int[] masses,
        int count,
        int factorMilli,
        int iterations)
    {
        if (positions == null)
            throw new ArgumentNullException(nameof(positions));
        if (radiiMm == null)
            throw new ArgumentNullException(nameof(radiiMm));
        if (masses == null)
            throw new ArgumentNullException(nameof(masses));
        if (count < 0
            || count > positions.Length
            || count > radiiMm.Length
            || count > masses.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (iterations <= 0 || factorMilli <= 0 || count < 2)
            return;

        int clampedFactor = Math.Min(FactorMilliMax, Math.Max(0, factorMilli));

        for (int iter = 0; iter < iterations; iter++)
        {
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                    SeparatePair(positions, radiiMm, masses, i, j, clampedFactor);
            }
        }
    }

    /// <summary>一对圆盘沿中心连线按质量比分配推开量。</summary>
    static void SeparatePair(
        SimVec2[] positions,
        int[] radiiMm,
        int[] masses,
        int i,
        int j,
        int factorMilli)
    {
        int dx = positions[j].X - positions[i].X;
        int dz = positions[j].Z - positions[i].Z;
        int dist = LengthMm(dx, dz);
        int minDist = radiiMm[i] + radiiMm[j];
        if (minDist <= 0)
            return;

        int overlap = minDist - dist;
        if (overlap <= 0)
            return;

        bool immovableI = masses[i] <= ImmovableMass;
        bool immovableJ = masses[j] <= ImmovableMass;
        // 双方都不可推动：无法分离，保持原位
        if (immovableI && immovableJ)
            return;

        int nx;
        int nz;
        if (dist <= 0)
        {
            // 完全重合：稳定轴向，低索引被推向 -X，高索引 +X（质量比仍适用）
            nx = 1;
            nz = 0;
            dist = 1;
        }
        else
        {
            nx = dx;
            nz = dz;
        }

        // 本对总推开量（软）：overlap * factor / 1000
        int totalPush = (int)((long)overlap * factorMilli / FactorMilliMax);
        if (totalPush <= 0)
            totalPush = 1;

        int pushI;
        int pushJ;
        if (immovableI)
        {
            pushI = 0;
            pushJ = totalPush;
        }
        else if (immovableJ)
        {
            pushI = totalPush;
            pushJ = 0;
        }
        else
        {
            int massI = Math.Max(1, masses[i]);
            int massJ = Math.Max(1, masses[j]);
            int massSum = massI + massJ;
            // 质量越大分到的推量越小：pushI ∝ massJ
            pushI = (int)((long)totalPush * massJ / massSum);
            pushJ = totalPush - pushI;
        }

        ApplyPush(positions, i, -pushI, nx, nz, dist);
        ApplyPush(positions, j, pushJ, nx, nz, dist);
    }

    /// <summary>沿单位方向施加毫米位移；量化为 0 时强制 1mm 避免卡死。</summary>
    static void ApplyPush(SimVec2[] positions, int index, int signedPush, int nx, int nz, int dist)
    {
        if (signedPush == 0)
            return;

        int mag = Math.Abs(signedPush);
        int sign = signedPush < 0 ? -1 : 1;
        int pushX = (int)Math.Round(mag * (double)nx / dist, MidpointRounding.AwayFromZero) * sign;
        int pushZ = (int)Math.Round(mag * (double)nz / dist, MidpointRounding.AwayFromZero) * sign;
        if (pushX == 0 && pushZ == 0)
            pushX = (nx >= 0 ? 1 : -1) * sign;

        positions[index] = new SimVec2(
            positions[index].X + pushX,
            positions[index].Z + pushZ);
    }

    /// <summary>整数毫米向量长度（就近取整）。</summary>
    public static int LengthMm(int dx, int dz)
    {
        double length = Math.Sqrt((double)dx * dx + (double)dz * dz);
        return (int)Math.Round(length, MidpointRounding.AwayFromZero);
    }
}

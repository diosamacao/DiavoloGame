using System;

/// <summary>
/// 烘焙静态障碍碰撞世界：圆盘 vs AABB 轴分离滑墙；地面高度恒定（简单平面）。
/// </summary>
public sealed class SimStaticCollisionWorld : ISimCollisionWorld
{
    readonly SimStaticAabb[] _aabbs;
    readonly int _groundYMm;

    /// <summary>用地面高度与 AABB 列表创建；数组会被复制，调用方可丢弃源。</summary>
    public SimStaticCollisionWorld(int groundYMm, SimStaticAabb[] aabbs)
    {
        _groundYMm = groundYMm;
        if (aabbs == null || aabbs.Length == 0)
        {
            _aabbs = Array.Empty<SimStaticAabb>();
            return;
        }

        _aabbs = new SimStaticAabb[aabbs.Length];
        Array.Copy(aabbs, _aabbs, aabbs.Length);
    }

    /// <summary>静态障碍数量。</summary>
    public int ObstacleCount => _aabbs.Length;

    /// <inheritdoc />
    public int GroundYMm => _groundYMm;

    /// <inheritdoc />
    public SimVec2 ResolveMove(SimVec2 fromMm, SimVec2 desiredMm, int radiusMm)
    {
        int r = Math.Max(0, radiusMm);
        SimVec2 start = Depenetrate(fromMm, r);
        int x = start.X;
        int z = start.Z;

        if (desiredMm.X != x)
            x = MoveAxisX(x, z, desiredMm.X, r);
        if (desiredMm.Z != z)
            z = MoveAxisZ(x, z, desiredMm.Z, r);

        return new SimVec2(x, z);
    }

    /// <summary>若圆心已陷入膨胀盒，推到最近边缘外（毫米）。</summary>
    public SimVec2 Depenetrate(SimVec2 positionMm, int radiusMm)
    {
        int x = positionMm.X;
        int z = positionMm.Z;
        int r = Math.Max(0, radiusMm);

        for (int i = 0; i < _aabbs.Length; i++)
        {
            SimStaticAabb expanded = _aabbs[i].Expanded(r);
            if (x < expanded.MinXMm || x > expanded.MaxXMm || z < expanded.MinZMm || z > expanded.MaxZMm)
                continue;

            int distMinX = x - expanded.MinXMm;
            int distMaxX = expanded.MaxXMm - x;
            int distMinZ = z - expanded.MinZMm;
            int distMaxZ = expanded.MaxZMm - z;
            int minDist = Math.Min(Math.Min(distMinX, distMaxX), Math.Min(distMinZ, distMaxZ));

            if (minDist == distMinX)
                x = expanded.MinXMm;
            else if (minDist == distMaxX)
                x = expanded.MaxXMm;
            else if (minDist == distMinZ)
                z = expanded.MinZMm;
            else
                z = expanded.MaxZMm;
        }

        return new SimVec2(x, z);
    }

    int MoveAxisX(int x, int z, int targetX, int radiusMm)
    {
        int best = targetX;
        bool movingPositive = targetX > x;

        for (int i = 0; i < _aabbs.Length; i++)
        {
            SimStaticAabb expanded = _aabbs[i].Expanded(radiusMm);
            if (z < expanded.MinZMm || z > expanded.MaxZMm)
                continue;

            if (movingPositive)
            {
                // 从左侧撞上膨胀盒左缘
                if (x <= expanded.MinXMm && best > expanded.MinXMm)
                    best = expanded.MinXMm;
            }
            else if (x >= expanded.MaxXMm && best < expanded.MaxXMm)
            {
                best = expanded.MaxXMm;
            }
        }

        return best;
    }

    int MoveAxisZ(int x, int z, int targetZ, int radiusMm)
    {
        int best = targetZ;
        bool movingPositive = targetZ > z;

        for (int i = 0; i < _aabbs.Length; i++)
        {
            SimStaticAabb expanded = _aabbs[i].Expanded(radiusMm);
            if (x < expanded.MinXMm || x > expanded.MaxXMm)
                continue;

            if (movingPositive)
            {
                if (z <= expanded.MinZMm && best > expanded.MinZMm)
                    best = expanded.MinZMm;
            }
            else if (z >= expanded.MaxZMm && best < expanded.MaxZMm)
            {
                best = expanded.MaxZMm;
            }
        }

        return best;
    }
}

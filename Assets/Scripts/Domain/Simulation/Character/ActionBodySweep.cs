using System;
using System.Collections.Generic;

/// <summary>动作直线路径的静态/圆盘连续检测；无跨帧状态，不补偿被截断的位移。</summary>
public static class ActionBodySweep
{
    // 修改几何语义时同步变化，使不同算法版本不能以相同内容指纹互联。
    public const int RulesVersion = 1;

    /// <summary>解析安全终点；先恢复静态非法起点，再求最早接触。blocker 无效且 blocked 为真表示静态阻挡。</summary>
    public static SimVec2 Resolve(ISimCollisionWorld world, SimVec2 from, SimVec2 delta,
        int radiusMm, int skinMm, IReadOnlyList<SimBodyObstacle> bodies,
        out bool blocked, out SimActorId blocker)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        if (radiusMm < 0 || skinMm < 0) throw new ArgumentOutOfRangeException(nameof(radiusMm));
        SimVec2 start = world.Depenetrate(from, radiusMm);
        double fraction = world.SweepFraction(start, delta, radiusMm);
        blocker = SimActorId.Invalid;
        for (int i = 0; bodies != null && i < bodies.Count; i++)
        {
            SimBodyObstacle body = bodies[i];
            double hit = CircleFraction(start, delta, body.PositionMm,
                (double)radiusMm + body.RadiusMm + skinMm);
            if (hit < fraction || (hit < 1 && hit == fraction && blocker.IsValid
                && body.ActorId.Value < blocker.Value))
            {
                fraction = hit;
                blocker = body.ActorId;
            }
        }

        blocked = fraction < 1;
        // 向量分量就近量化后重新扫掠，避免斜向取整穿入接触面或把切线变成割线。
        // 若量化后不安全，按路径退 1mm 再试；极端擦边最多四次，仍不安全则本帧停留。
        double length = Math.Sqrt((double)delta.X * delta.X + (double)delta.Z * delta.Z);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            var allowed = new SimVec2(Round(delta.X * fraction), Round(delta.Z * fraction));
            bool safe = world.SweepFraction(start, allowed, radiusMm) >= 1;
            for (int i = 0; safe && bodies != null && i < bodies.Count; i++)
                safe = CircleFraction(start, allowed, bodies[i].PositionMm,
                    (double)radiusMm + bodies[i].RadiusMm + skinMm) >= 1;
            if (safe)
                return new SimVec2(checked(start.X + allowed.X), checked(start.Z + allowed.Z));
            blocked = true;
            fraction = length > 0 ? Math.Max(0, fraction - 1 / length) : 0;
        }
        return start;
    }

    /// <summary>点扫膨胀圆的最早入射比例；初始重叠仅拦截向内分量，允许向外/切向逃离。</summary>
    static double CircleFraction(SimVec2 from, SimVec2 delta, SimVec2 center, double radius)
    {
        double x = (double)from.X - center.X, z = (double)from.Z - center.Z;
        double a = (double)delta.X * delta.X + (double)delta.Z * delta.Z;
        if (a == 0) return 1;
        double b = x * delta.X + z * delta.Z;
        double c = x * x + z * z - radius * radius;
        if (c <= 0) return b < 0 ? 0 : 1;
        if (b >= 0) return 1;
        double discriminant = b * b - a * c;
        if (discriminant <= 0) return 1; // 纯切线不进入圆内。
        // 等价于 (-b-sqrt(d))/a，避免接近表面时相减消去有效位。
        double t = c / (-b + Math.Sqrt(discriminant));
        return t >= 0 && t < 1 ? t : 1;
    }

    static int Round(double value) => checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
}

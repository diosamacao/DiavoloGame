/// <summary>逻辑层静态碰撞查询；禁止访问 Unity Physics。</summary>
public interface ISimCollisionWorld
{
    /// <summary>简单地面高度（毫米）；L2 为恒定平面，供 MotorSim 着地。</summary>
    int GroundYMm { get; }

    /// <summary>
    /// 将半径为 radiusMm 的水平圆盘从 from 移向 to，返回允许到达的终点（可滑墙）。
    /// </summary>
    SimVec2 ResolveMove(SimVec2 fromMm, SimVec2 desiredMm, int radiusMm);

    /// <summary>恢复静态重叠起点；不执行期望位移。</summary>
    SimVec2 Depenetrate(SimVec2 positionMm, int radiusMm);

    /// <summary>沿完整直线扫掠，返回最早入射比例 [0,1]；不滑墙、不写位置。调用前先脱嵌。</summary>
    double SweepFraction(SimVec2 fromMm, SimVec2 deltaMm, int radiusMm);
}

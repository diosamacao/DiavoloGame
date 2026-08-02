/// <summary>逻辑层静态碰撞查询；禁止访问 Unity Physics。</summary>
public interface ISimCollisionWorld
{
    /// <summary>
    /// 将半径为 radiusMm 的水平圆盘从 from 移向 to，返回允许到达的终点（可滑墙）。
    /// </summary>
    SimVec2 ResolveMove(SimVec2 fromMm, SimVec2 desiredMm, int radiusMm);
}

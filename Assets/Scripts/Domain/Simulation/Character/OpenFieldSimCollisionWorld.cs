/// <summary>空场地碰撞世界：不阻挡任何水平位移，供 MotorSim 首版与无障碍测试。</summary>
public sealed class OpenFieldSimCollisionWorld : ISimCollisionWorld
{
    /// <summary>共享实例；无状态可复用。</summary>
    public static readonly OpenFieldSimCollisionWorld Instance = new();

    /// <summary>直接接受目标点。</summary>
    public SimVec2 ResolveMove(SimVec2 fromMm, SimVec2 desiredMm, int radiusMm) => desiredMm;
}

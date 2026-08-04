/// <summary>空场地碰撞世界：不阻挡水平位移；地面 Y=0。无烘焙数据时的默认世界。</summary>
public sealed class OpenFieldSimCollisionWorld : ISimCollisionWorld
{
    /// <summary>共享实例；无状态可复用。</summary>
    public static readonly OpenFieldSimCollisionWorld Instance = new();

    /// <inheritdoc />
    public int GroundYMm => 0;

    /// <summary>直接接受目标点。</summary>
    public SimVec2 ResolveMove(SimVec2 fromMm, SimVec2 desiredMm, int radiusMm) => desiredMm;
}

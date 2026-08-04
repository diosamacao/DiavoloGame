using System;

/// <summary>
/// 可序列化的静态碰撞烘焙载荷（无 Unity 依赖）；由 Domain SO 持有并在运行时构建 World。
/// </summary>
[Serializable]
public sealed class StaticCollisionBakeData
{
    public int groundYMm;
    public SimStaticAabb[] aabbs = Array.Empty<SimStaticAabb>();
    public string sourceSceneName = string.Empty;
    public long bakedUtcTicks;

    /// <summary>障碍数量。</summary>
    public int ObstacleCount => aabbs?.Length ?? 0;

    /// <summary>构建纯逻辑碰撞世界；无障碍时仍保留地面高度。</summary>
    public ISimCollisionWorld CreateWorld()
    {
        if (aabbs == null || aabbs.Length == 0)
        {
            // 无障碍但可能有非零地面：用只含地面的静态世界
            if (groundYMm == 0)
                return OpenFieldSimCollisionWorld.Instance;
            return new SimStaticCollisionWorld(groundYMm, Array.Empty<SimStaticAabb>());
        }

        return new SimStaticCollisionWorld(groundYMm, aabbs);
    }
}

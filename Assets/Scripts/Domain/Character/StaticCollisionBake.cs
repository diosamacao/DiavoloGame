using UnityEngine;

/// <summary>
/// 场景静态碰撞烘焙资产：Editor 从 Collider 写入 AABB，运行时构建 ISimCollisionWorld。
/// </summary>
[CreateAssetMenu(
    fileName = "StaticCollisionBake",
    menuName = "ACTGame/Simulation/Static Collision Bake")]
public sealed class StaticCollisionBake : ScriptableObject
{
    [SerializeField] StaticCollisionBakeData data = new();

    /// <summary>烘焙载荷；供 Editor 写回与运行时建 World。</summary>
    public StaticCollisionBakeData Data => data ??= new StaticCollisionBakeData();

    /// <summary>用当前数据创建逻辑碰撞世界。</summary>
    public ISimCollisionWorld CreateWorld() => Data.CreateWorld();

#if UNITY_EDITOR
    /// <summary>Editor 写回烘焙结果。</summary>
    public void EditorSetData(StaticCollisionBakeData source)
    {
        data ??= new StaticCollisionBakeData();
        if (source == null)
        {
            data.groundYMm = 0;
            data.aabbs = System.Array.Empty<SimStaticAabb>();
            data.sourceSceneName = string.Empty;
            data.bakedUtcTicks = 0;
            return;
        }

        data.groundYMm = source.groundYMm;
        data.sourceSceneName = source.sourceSceneName ?? string.Empty;
        data.bakedUtcTicks = source.bakedUtcTicks;
        if (source.aabbs == null || source.aabbs.Length == 0)
        {
            data.aabbs = System.Array.Empty<SimStaticAabb>();
        }
        else
        {
            data.aabbs = new SimStaticAabb[source.aabbs.Length];
            System.Array.Copy(source.aabbs, data.aabbs, source.aabbs.Length);
        }
    }
#endif
}

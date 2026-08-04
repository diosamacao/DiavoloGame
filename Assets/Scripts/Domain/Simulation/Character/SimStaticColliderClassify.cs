using System;

/// <summary>
/// 静态烘焙分类：区分地面薄板（只贡献 GroundY）与墙体障碍（水平 AABB 硬挡）。
/// </summary>
public static class SimStaticColliderClassify
{
    /// <summary>默认：竖直厚度不超过该米数才可能判为地面。</summary>
    public const float DefaultMaxFloorHeightMeters = 0.75f;

    /// <summary>
    /// 是否为地面型包围盒：竖直很薄，且明显扁于水平尺寸。
    /// 大型 Floor 平面不得进水平硬挡，否则会把整块场地变成墙并把角色挤出。
    /// </summary>
    public static bool IsFloorLikeBounds(
        float sizeXMeters,
        float sizeYMeters,
        float sizeZMeters,
        float maxFloorHeightMeters = DefaultMaxFloorHeightMeters)
    {
        if (sizeXMeters <= 0f || sizeYMeters <= 0f || sizeZMeters <= 0f)
            return false;

        float maxH = maxFloorHeightMeters > 0f ? maxFloorHeightMeters : DefaultMaxFloorHeightMeters;
        if (sizeYMeters > maxH)
            return false;

        // 水平较小边；地面应「扁」——厚度显著小于平面跨度
        float horizontalMin = Math.Min(sizeXMeters, sizeZMeters);
        return sizeYMeters <= horizontalMin * 0.35f;
    }

    /// <summary>名称是否像地面（Floor/Ground/Terrain，忽略大小写）。</summary>
    public static bool IsFloorLikeName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        return ContainsIgnoreCase(objectName, "floor")
               || ContainsIgnoreCase(objectName, "ground")
               || ContainsIgnoreCase(objectName, "terrain");
    }

    static bool ContainsIgnoreCase(string source, string token)
    {
        return source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

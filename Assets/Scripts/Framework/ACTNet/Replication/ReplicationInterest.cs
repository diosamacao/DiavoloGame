/// <summary>连接级兴趣：Owner/玩家始终可见，敌人按平面距离裁剪。</summary>
public static class ReplicationInterest
{
    /// <summary>默认兴趣半径 40m。</summary>
    public const int DefaultRadiusMm = 40000;

    /// <summary>判断实体是否应进入该连接的 full set；半径 ≤0 表示不裁剪。</summary>
    public static bool IsRelevant(bool isOwner, bool isPlayer, int deltaXMm, int deltaZMm, int radiusMm)
    {
        if (isOwner || isPlayer)
            return true;
        if (radiusMm <= 0)
            return true;

        long dx = deltaXMm;
        long dz = deltaZMm;
        long radius = radiusMm;
        return dx * dx + dz * dz <= radius * radius;
    }
}

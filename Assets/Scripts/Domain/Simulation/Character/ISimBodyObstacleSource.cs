/// <summary>从已提交逻辑状态读取实体体积，不读取表现 Pose 或受击盒。</summary>
public interface ISimBodyObstacleSource
{
    /// <summary>死亡、离场或停用时返回 false；软体抑制和无敌不移除实体。</summary>
    bool TryGetBodyObstacle(out SimBodyObstacle obstacle);
}

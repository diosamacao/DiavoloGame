using System.Collections.Generic;

/// <summary>为一次动作提交冻结身体列表；实现拥有注册表，调用方拥有可复用缓冲。</summary>
public interface ISimBodyObstacleQuery
{
    /// <summary>清空并填入除自身外的实体，按稳定 Id 排序；不修改被查询角色。</summary>
    void Collect(SimActorId selfId, List<SimBodyObstacle> results);
}

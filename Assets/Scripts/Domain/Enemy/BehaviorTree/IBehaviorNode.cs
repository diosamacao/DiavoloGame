/// <summary>自研行为树节点接口；仅 Native Runner 内部使用，插件后端不必实现。</summary>
public interface IBehaviorNode
{
    /// <summary>推进本节点一帧。</summary>
    BehaviorStatus Tick(EnemyBlackboard blackboard);

    /// <summary>清空 Running 子索引与 Wait 计时。</summary>
    void Reset();
}

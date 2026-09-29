/// <summary>行为树可注入 RNG（RandomSelector 等）；EditMode 可固定序列。</summary>
public interface IEnemyBehaviorRandom
{
    /// <summary>返回 [0, 1) 均匀随机数。</summary>
    float NextUnit();
}

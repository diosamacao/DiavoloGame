
/// <summary>基于 <see cref="System.Random"/> 的可播种实现。</summary>
public sealed class SystemEnemyBehaviorRandom : IEnemyBehaviorRandom
{
    readonly System.Random _rng;

    /// <summary>创建可播种 RNG。</summary>
    public SystemEnemyBehaviorRandom(int seed)
    {
        _rng = new System.Random(seed);
    }

    /// <summary>无参：非确定性默认种子。</summary>
    public SystemEnemyBehaviorRandom()
    {
        _rng = new System.Random();
    }

    /// <inheritdoc />
    public float NextUnit() => (float)_rng.NextDouble();
}

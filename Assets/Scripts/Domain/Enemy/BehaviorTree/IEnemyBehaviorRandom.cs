/// <summary>行为树可注入 RNG（RandomSelector 等）；EditMode 可固定序列。</summary>
public interface IEnemyBehaviorRandom
{
    /// <summary>返回 [0, 1) 均匀随机数。</summary>
    float NextUnit();
}

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

/// <summary>按给定序列循环返回 NextUnit（单测用）。</summary>
public sealed class SequenceEnemyBehaviorRandom : IEnemyBehaviorRandom
{
    readonly float[] _values;
    int _index;

    /// <summary>values 元素须在 [0,1)；空则恒返回 0。</summary>
    public SequenceEnemyBehaviorRandom(params float[] values)
    {
        _values = values != null && values.Length > 0
            ? values
            : new[] { 0f };
    }

    /// <inheritdoc />
    public float NextUnit()
    {
        float v = _values[_index % _values.Length];
        _index++;
        return v;
    }
}

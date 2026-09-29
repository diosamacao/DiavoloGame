
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

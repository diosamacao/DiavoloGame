/// <summary>
/// RandomSelector：按权重抽一个子节点执行；Running 期间粘住同一子。
/// RNG：构造注入 &gt; 黑板 Rng &gt; 共享 SystemRandom。
/// </summary>
public sealed class RandomSelectorNode : IBehaviorNode
{
    static readonly IEnemyBehaviorRandom SharedFallback = new SystemEnemyBehaviorRandom(1);

    readonly IBehaviorNode[] _children;
    readonly float[] _weights;
    readonly IEnemyBehaviorRandom _injected;
    int _runningIndex = -1;

    /// <summary>创建权重随机选择；weights 可短于 children（缺省按 1）。</summary>
    public RandomSelectorNode(
        IBehaviorNode[] children,
        float[] weights,
        IEnemyBehaviorRandom random = null)
    {
        _children = children ?? System.Array.Empty<IBehaviorNode>();
        _weights = NormalizeWeights(_children.Length, weights);
        _injected = random;
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (_children.Length == 0)
            return BehaviorStatus.Failure;

        if (_runningIndex < 0)
        {
            int pick = PickWeighted(ResolveRng(blackboard));
            if (pick < 0)
                return BehaviorStatus.Failure;
            _runningIndex = pick;
        }

        BehaviorStatus status = _children[_runningIndex].Tick(blackboard);
        if (status == BehaviorStatus.Running)
            return BehaviorStatus.Running;

        // 子完成：清粘滞索引，下次 Tick 重新抽签
        _runningIndex = -1;
        return status;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _runningIndex = -1;
        for (int i = 0; i < _children.Length; i++)
            _children[i].Reset();
    }

    IEnemyBehaviorRandom ResolveRng(EnemyBlackboard blackboard) =>
        _injected ?? blackboard?.Rng ?? SharedFallback;

    /// <summary>按权重抽样；总权≤0 返回 -1。</summary>
    int PickWeighted(IEnemyBehaviorRandom rng)
    {
        float total = 0f;
        for (int i = 0; i < _weights.Length; i++)
            total += _weights[i];
        if (total <= 0f)
            return -1;

        float roll = rng.NextUnit() * total;
        if (roll < 0f)
            roll = 0f;

        float acc = 0f;
        for (int i = 0; i < _weights.Length; i++)
        {
            acc += _weights[i];
            // 右开区间：roll∈[0,total) 落在首个 acc>roll 的桶
            if (roll < acc)
                return i;
        }

        return _weights.Length - 1;
    }

    static float[] NormalizeWeights(int count, float[] weights)
    {
        var result = new float[count];
        for (int i = 0; i < count; i++)
        {
            float w = weights != null && i < weights.Length ? weights[i] : 1f;
            result[i] = w > 0f ? w : 0f;
        }

        return result;
    }
}

/// <summary>Selector：子节点按序，首个非 Failure 决定结果。</summary>
public sealed class SelectorNode : IBehaviorNode
{
    readonly IBehaviorNode[] _children;
    int _runningIndex;

    /// <summary>创建 Selector。</summary>
    public SelectorNode(params IBehaviorNode[] children)
    {
        _children = children ?? System.Array.Empty<IBehaviorNode>();
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        for (int i = _runningIndex; i < _children.Length; i++)
        {
            BehaviorStatus status = _children[i].Tick(blackboard);
            if (status == BehaviorStatus.Running)
            {
                _runningIndex = i;
                return BehaviorStatus.Running;
            }

            if (status == BehaviorStatus.Success)
            {
                _runningIndex = 0;
                return BehaviorStatus.Success;
            }
        }

        _runningIndex = 0;
        return BehaviorStatus.Failure;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _runningIndex = 0;
        for (int i = 0; i < _children.Length; i++)
            _children[i].Reset();
    }
}

/// <summary>Sequence：子节点按序，任一 Failure 则失败；Running 记住索引。</summary>
public sealed class SequenceNode : IBehaviorNode
{
    readonly IBehaviorNode[] _children;
    int _runningIndex;

    /// <summary>创建 Sequence。</summary>
    public SequenceNode(params IBehaviorNode[] children)
    {
        _children = children ?? System.Array.Empty<IBehaviorNode>();
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        for (int i = _runningIndex; i < _children.Length; i++)
        {
            BehaviorStatus status = _children[i].Tick(blackboard);
            if (status == BehaviorStatus.Running)
            {
                _runningIndex = i;
                return BehaviorStatus.Running;
            }

            if (status == BehaviorStatus.Failure)
            {
                _runningIndex = 0;
                return BehaviorStatus.Failure;
            }
        }

        _runningIndex = 0;
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _runningIndex = 0;
        for (int i = 0; i < _children.Length; i++)
            _children[i].Reset();
    }
}

using System;
using UnityEngine;

/// <summary>
/// UE 风格条件装饰器：先求值，失败则 Abort Self（Reset 子树）并 Failure；通过则 Tick 子节点。
/// </summary>
public abstract class ConditionalDecoratorNode : IBehaviorNode
{
    readonly IBehaviorNode _child;

    /// <summary>创建条件装饰；child 不可空。</summary>
    protected ConditionalDecoratorNode(IBehaviorNode child)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
    }

    /// <summary>本帧条件是否成立。</summary>
    protected abstract bool Evaluate(EnemyBlackboard blackboard);

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (!Evaluate(blackboard))
        {
            // Abort Self：条件翻面时清掉 Running 子进度，避免恢复后接着跑
            _child.Reset();
            return BehaviorStatus.Failure;
        }

        return _child.Tick(blackboard);
    }

    /// <inheritdoc />
    public virtual void Reset() => _child.Reset();
}

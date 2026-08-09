/// <summary>命名装饰：用 NodeName 写入黑板路径（Gizmo / Graph 高亮）。</summary>
public sealed class NamedNode : IBehaviorNode
{
    readonly string _name;
    readonly IBehaviorNode _child;

    /// <summary>创建命名装饰节点。</summary>
    public NamedNode(string name, IBehaviorNode child)
    {
        _name = string.IsNullOrEmpty(name) ? "Node" : name;
        _child = child ?? throw new System.ArgumentNullException(nameof(child));
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        BehaviorStatus status = _child.Tick(blackboard);
        blackboard?.AppendDebug(_name, status);
        return status;
    }

    /// <inheritdoc />
    public void Reset() => _child.Reset();
}

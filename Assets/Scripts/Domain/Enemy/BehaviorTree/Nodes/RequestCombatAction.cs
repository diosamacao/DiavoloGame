using UnityEngine;

/// <summary>行动：请求按 Graph Entry NodeId 起手（Brain → CombatRequestBuffer → Driver）。</summary>
public sealed class RequestCombatAction : IBehaviorNode
{
    readonly string _entryNodeId;

    /// <summary>创建 Entry 起手请求；entryNodeId 不可空。</summary>
    public RequestCombatAction(string entryNodeId)
    {
        _entryNodeId = entryNodeId ?? string.Empty;
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard == null || string.IsNullOrEmpty(_entryNodeId))
            return BehaviorStatus.Failure;

        blackboard.HasCombatRequest = true;
        blackboard.CombatRequestEntryId = _entryNodeId;
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset()
    {
    }
}

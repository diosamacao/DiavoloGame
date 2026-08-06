/// <summary>把 ActionDefinition.ResourceSpec 接到 CharacterResourceSim；供 Driver/ActionSim 鉴权扣费。</summary>
public sealed class ActionResourceGate : IActionResourceGate
{
    readonly CharacterResourceSim _resources;

    /// <summary>绑定角色资源权威。</summary>
    public ActionResourceGate(CharacterResourceSim resources)
    {
        _resources = resources ?? throw new System.ArgumentNullException(nameof(resources));
    }

    /// <inheritdoc />
    public bool CanAfford(IActionSimContent content)
    {
        ActionResourceSpec spec = ResolveSpec(content);
        return _resources.CanAfford(spec);
    }

    /// <inheritdoc />
    public void CommitCost(IActionSimContent content)
    {
        ActionResourceSpec spec = ResolveSpec(content);
        _resources.CommitCost(spec);
    }

    static ActionResourceSpec ResolveSpec(IActionSimContent content)
    {
        if (content is ActionDefinition action)
            return action.ResourceSpec ?? ActionResourceSpec.Empty;
        return ActionResourceSpec.Empty;
    }
}

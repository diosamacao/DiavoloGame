using System;

/// <summary>把 ActionDefinition.ResourceSpec 接到 NumericSystem（Spec→Instant Cost Effect）。</summary>
public sealed class NumericCostGate : IActionResourceGate
{
    readonly NumericSystem _numeric;

    /// <summary>绑定角色数值中枢。</summary>
    public NumericCostGate(NumericSystem numeric)
    {
        _numeric = numeric ?? throw new ArgumentNullException(nameof(numeric));
    }

    /// <inheritdoc />
    public bool CanAfford(IActionSimContent content) =>
        ActionResourceSpecEffectCompiler.CanAfford(_numeric, ResolveSpec(content));

    /// <inheritdoc />
    public void CommitCost(IActionSimContent content) =>
        ActionResourceSpecEffectCompiler.ApplyCost(_numeric, ResolveSpec(content));

    static ActionResourceSpec ResolveSpec(IActionSimContent content)
    {
        if (content is ActionDefinition action)
            return action.ResourceSpec ?? ActionResourceSpec.Empty;
        return ActionResourceSpec.Empty;
    }
}

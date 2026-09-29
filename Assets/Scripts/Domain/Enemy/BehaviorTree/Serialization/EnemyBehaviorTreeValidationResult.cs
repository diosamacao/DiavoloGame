using System.Collections.Generic;

/// <summary>行为树资产 / 根校验结果。</summary>
public sealed class EnemyBehaviorTreeValidationResult
{
    readonly List<string> _errors = new List<string>();
    readonly List<string> _warnings = new List<string>();

    /// <summary>无 Error 即为通过（Warning 可保留）。</summary>
    public bool IsValid => _errors.Count == 0;

    /// <summary>错误列表。</summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>警告列表。</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>追加错误。</summary>
    public void AddError(string message)
    {
        if (!string.IsNullOrEmpty(message))
            _errors.Add(message);
    }

    /// <summary>追加警告。</summary>
    public void AddWarning(string message)
    {
        if (!string.IsNullOrEmpty(message))
            _warnings.Add(message);
    }
}

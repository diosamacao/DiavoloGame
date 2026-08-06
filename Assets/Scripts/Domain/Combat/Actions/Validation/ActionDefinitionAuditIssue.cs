/// <summary>单条 Action 审计问题严重度。</summary>
public enum ActionDefinitionAuditSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
}

/// <summary>单条 Action 审计问题。</summary>
public readonly struct ActionDefinitionAuditIssue
{
    /// <summary>创建一条审计问题。</summary>
    public ActionDefinitionAuditIssue(ActionDefinitionAuditSeverity severity, string code, string message)
    {
        Severity = severity;
        Code = code ?? string.Empty;
        Message = message ?? string.Empty;
    }

    /// <summary>严重度。</summary>
    public ActionDefinitionAuditSeverity Severity { get; }

    /// <summary>稳定错误码，便于过滤与归档。</summary>
    public string Code { get; }

    /// <summary>人类可读说明。</summary>
    public string Message { get; }
}

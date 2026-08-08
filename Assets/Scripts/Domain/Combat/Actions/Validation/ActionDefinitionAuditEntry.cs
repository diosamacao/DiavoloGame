using System;
using System.Collections.Generic;

/// <summary>单个 ActionDefinition 的位移源归类与校验结果。</summary>
public sealed class ActionDefinitionAuditEntry
{
    readonly List<ActionDefinitionAuditIssue> _issues = new(4);

    /// <summary>资产路径。</summary>
    public string AssetPath { get; set; } = string.Empty;

    /// <summary>资产名。</summary>
    public string ActionName { get; set; } = string.Empty;

    /// <summary>三源归类结果。</summary>
    public ActionMotionSourceKind MotionSourceKind { get; set; }

    /// <summary>烘焙表是否就绪。</summary>
    public bool BakedReady { get; set; }

    /// <summary>Timeline 是否存在脚本位移。</summary>
    public bool HasScriptedMovement { get; set; }

    /// <summary>ExecutionPolicy.BaseMotionMode。</summary>
    public ActionBaseMotionMode BaseMotionMode { get; set; }

    /// <summary>声明 sampleRate。</summary>
    public int SampleRate { get; set; }

    /// <summary>声明 totalFrames。</summary>
    public int TotalFrames { get; set; }

    /// <summary>烘焙 logicHz（未就绪可为 0）。</summary>
    public int BakedLogicHz { get; set; }

    /// <summary>烘焙 frameCount（未就绪可为 0）。</summary>
    public int BakedFrameCount { get; set; }

    /// <summary>问题列表。</summary>
    public IReadOnlyList<ActionDefinitionAuditIssue> Issues => _issues;

    /// <summary>是否含 Error 级问题。</summary>
    public bool HasError
    {
        get
        {
            for (int i = 0; i < _issues.Count; i++)
            {
                if (_issues[i].Severity == ActionDefinitionAuditSeverity.Error)
                    return true;
            }

            return false;
        }
    }

    /// <summary>追加问题。</summary>
    public void AddIssue(ActionDefinitionAuditSeverity severity, string code, string message) =>
        _issues.Add(new ActionDefinitionAuditIssue(severity, code, message));
}

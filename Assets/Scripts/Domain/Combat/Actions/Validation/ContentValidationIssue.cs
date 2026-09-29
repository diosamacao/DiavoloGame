using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>可供运行时门禁和 Editor 定位共用的问题，不依赖 UnityEditor。</summary>
public readonly struct ContentValidationIssue
{
    public ContentValidationIssue(string code, UnityEngine.Object asset, string propertyPath,
        string message, string nodeId = null, ContentIssueSeverity severity = ContentIssueSeverity.Error)
    {
        Code = code; Asset = asset; PropertyPath = propertyPath; Message = message;
        NodeId = nodeId; Severity = severity;
    }
    public string Code { get; }
    public UnityEngine.Object Asset { get; }
    public string PropertyPath { get; }
    public string Message { get; }
    public string NodeId { get; }
    public ContentIssueSeverity Severity { get; }
    public override string ToString() => $"[{Code}] {Message} ({PropertyPath})";
}

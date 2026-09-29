using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>匹配失败条目。</summary>
public readonly struct MotionClipMatchIssue
{
    public MotionClipMatchIssue(string inplacePath, string inplaceName, string reason)
    {
        InplacePath = inplacePath;
        InplaceName = inplaceName;
        Reason = reason;
    }

    public string InplacePath { get; }
    public string InplaceName { get; }
    public string Reason { get; }
}

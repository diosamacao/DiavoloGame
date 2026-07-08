using System;
using UnityEngine;

/// <summary>通用动作点事件：用于自定义信号、音效、镜头等非专用 Notify。</summary>
[Serializable]
public class ActionEvent : ActionNotify
{
    [SerializeField] ActionEventKind kind = ActionEventKind.Custom;
    [SerializeField] string payloadId = string.Empty;

    public string EventId => Id;
    public ActionEventKind Kind => kind;
    public string PayloadId => payloadId;
}

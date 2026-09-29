using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>ActionDefinition 编辑器 Scene 预览上下文；供动画采样与各 PreviewExtension 只读消费。</summary>
public readonly struct ActionEditorPreviewContext
{
    public ActionEditorPreviewContext(
        ActionDefinition action,
        Transform previewCharacter,
        Transform attachPoint,
        int previewFrame)
    {
        Action = action;
        PreviewCharacter = previewCharacter;
        AttachPoint = attachPoint;
        PreviewFrame = previewFrame;
        // 无 Action 时仍按全局逻辑 Hz，避免误用旧 30Hz 估时
        SampleRate = action != null ? action.SampleRate : ActionSim.LogicHz;
        PreviewTimeSeconds = SampleRate > 0f ? previewFrame / SampleRate : 0f;
    }

    public ActionDefinition Action { get; }
    public Transform PreviewCharacter { get; }
    public Transform AttachPoint { get; }
    public int PreviewFrame { get; }
    public float SampleRate { get; }
    public float PreviewTimeSeconds { get; }

    public bool IsValid =>
        Action != null
        && PreviewCharacter != null
        && Action.HasAnimation;
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ActionDefinition 编辑器 Scene 预览扩展点；新增刀光/HITSTOP/镜头等预览时实现此接口并注册到 Session。
/// </summary>
public interface IActionEditorPreviewExtension
{
    /// <summary>Preview Character 与 Action 就绪后调用。</summary>
    void OnPreviewBegin(in ActionEditorPreviewContext context);

    /// <summary>每 Editor 帧调用；动画采样完成后执行，AttachPoint 已与当前帧 Pose 对齐。</summary>
    void OnPreviewUpdate(in ActionEditorPreviewContext context);

    /// <summary>Session 结束或 Preview Character 移除时清理临时对象。</summary>
    void OnPreviewEnd(in ActionEditorPreviewContext context);
}

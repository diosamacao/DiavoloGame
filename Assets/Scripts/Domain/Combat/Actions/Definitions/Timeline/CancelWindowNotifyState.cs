using System;
using UnityEngine;

/// <summary>
/// 每个 Action 唯一的取消窗口；Perfect 分割帧之前走 Cancel，分割帧及之后走 PerfectCancel。
/// </summary>
[Serializable]
public class CancelWindowNotifyState : ActionNotifyState
{
    [Tooltip("小于 0 表示没有 Perfect 区间；有效值会限制在 CancelWindow 内。")]
    [SerializeField] int perfectFrame = -1;

    /// <summary>Perfect 区间起始帧；小于 0 表示仅有普通 Cancel。</summary>
    public int PerfectFrame => perfectFrame;

    /// <summary>窗口是否被有效 Perfect 帧划分为两段。</summary>
    public bool HasPerfectSplit =>
        perfectFrame >= StartFrame && perfectFrame <= EndFrame;

    /// <summary>按当前帧返回普通或 Perfect 路由；调用方应先确认窗口有效。</summary>
    public ActionCancelRouteKind ResolveRouteAtFrame(int frame) =>
        HasPerfectSplit && frame >= perfectFrame
            ? ActionCancelRouteKind.PerfectCancel
            : ActionCancelRouteKind.Cancel;

    /// <summary>将 Perfect 分割帧限制在窗口内；负值保持“未配置”。</summary>
    public void ClampPerfectFrame()
    {
        if (perfectFrame >= 0)
            perfectFrame = Mathf.Clamp(perfectFrame, StartFrame, EndFrame);
    }
}

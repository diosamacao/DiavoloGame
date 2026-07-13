using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>取消窗口区间；Action/Recovery 取消走 Resolver，Movement 允许退回移动。</summary>
[Serializable]
public class CancelWindowNotifyState : ActionNotifyState
{
    [SerializeField] CancelType cancelType = CancelType.Action;

    /// <summary>窗口取消类型：连招进位、后摇重开或移动取消。</summary>
    public CancelType CancelType => cancelType;

    /// <summary>
    /// Cancel 槽稳定 id，供 ActionGraph 边绑定；复用时间轴条目 Id。
    /// 改帧不改 Id；改 Id 会断开图边。
    /// </summary>
    public string CancelSlotId => Id;

    /// <summary>转为运行时只读窗口。</summary>
    public ResolvedCancelWindow ToResolved() =>
        new(StartFrame, EndFrame, cancelType, CancelSlotId, Priority);
}

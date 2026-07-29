using System;
using UnityEngine;

/// <summary>独立取消窗口；类型决定走 Normal 或 Perfect 图路由。</summary>
[Serializable]
public class CancelWindowNotifyState : ActionNotifyState
{
    [Tooltip("Normal 为普通派生；Perfect 与 Normal 重叠且节点 Intent 相同时优先。")]
    [SerializeField] CancelWindowType windowType = CancelWindowType.Normal;

    /// <summary>窗口对应的 Normal 或 Perfect 图路由。</summary>
    public CancelWindowType WindowType => windowType;
}

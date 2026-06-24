using UnityEngine;

/// <summary>卡肉开始事件；反馈系统广播给 VFX 等需要暂停自身时间的表现系统。</summary>
public readonly struct HitStopBeganEvent
{
    /// <summary>创建卡肉开始事件。</summary>
    public HitStopBeganEvent(Transform attackerRoot)
    {
        AttackerRoot = attackerRoot;
    }

    /// <summary>触发卡肉的攻击者根节点。</summary>
    public Transform AttackerRoot { get; }
}

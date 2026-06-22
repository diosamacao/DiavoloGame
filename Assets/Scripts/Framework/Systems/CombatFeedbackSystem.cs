using UnityEngine;

/// <summary>架构级战斗反馈状态系统；管理卡肉全局状态并分发反馈事件。</summary>
public sealed class CombatFeedbackSystem : IArchitectureSystem
{
    ACTGameArchitecture _architecture;

    /// <summary>当前是否处于卡肉中。</summary>
    public bool IsHitStopActive { get; private set; }

    /// <summary>当前卡肉对应的攻击者根节点。</summary>
    public Transform ActiveHitStopAttackerRoot { get; private set; }

    /// <summary>初始化反馈系统。</summary>
    public void Initialize(ACTGameArchitecture architecture)
    {
        _architecture = architecture;
    }

    /// <summary>进入卡肉并广播开始事件。</summary>
    public void BeginHitStop(Transform attackerRoot)
    {
        IsHitStopActive = true;
        ActiveHitStopAttackerRoot = attackerRoot;
        _architecture.SendEvent(new HitStopBeganEvent(attackerRoot));
    }

    /// <summary>结束卡肉并广播结束事件。</summary>
    public void EndHitStop()
    {
        if (!IsHitStopActive)
            return;

        IsHitStopActive = false;
        ActiveHitStopAttackerRoot = null;
        _architecture.SendEvent(HitStopEndedEvent.Instance);
    }
}

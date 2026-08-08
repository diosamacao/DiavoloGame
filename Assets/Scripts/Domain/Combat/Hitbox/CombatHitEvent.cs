using UnityEngine;

/// <summary>Collect 阶段产生的命中事件；稳定键参与排序，Unity 引用只供当前 L0C 结算桥接。</summary>
public readonly struct CombatHitEvent
{
    /// <summary>创建一条待帧末结算的命中事件。</summary>
    public CombatHitEvent(
        SimHitKey key,
        ActionHitContext context,
        IHurtboxTarget target,
        IActionSimHitReceiver hitReceiver,
        Transform targetTransform,
        Vector3 hitPoint)
    {
        Key = key;
        Context = context;
        Target = target;
        HitReceiver = hitReceiver;
        TargetTransform = targetTransform;
        HitPoint = hitPoint;
    }

    /// <summary>纯模拟稳定排序与去重键。</summary>
    public SimHitKey Key { get; }

    /// <summary>当前动作系统伤害与反馈上下文。</summary>
    public ActionHitContext Context { get; }

    /// <summary>帧末接收伤害的目标。</summary>
    public IHurtboxTarget Target { get; }

    /// <summary>帧末接收命中确认的攻击者。</summary>
    public IActionSimHitReceiver HitReceiver { get; }

    /// <summary>表现层生成命中方向所需的目标根。</summary>
    public Transform TargetTransform { get; }

    /// <summary>表现用接触点（攻击盒中心→受击盒最近点）；不参与伤害权威。</summary>
    public Vector3 HitPoint { get; }
}

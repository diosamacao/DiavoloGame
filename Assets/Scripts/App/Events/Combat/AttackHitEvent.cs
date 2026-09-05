using UnityEngine;

/// <summary>攻击命中事件；命中结算完成后广播给反馈、相机、VFX、音频与 UI 系统。</summary>
public readonly struct AttackHitEvent : IArchitectureEvent
{
    /// <summary>创建攻击命中事件。弹刀与完美闪避旗标分开，卡肉帧由 Pipeline 裁定。</summary>
    public AttackHitEvent(
        ActionHitContext context,
        Transform targetTransform,
        Vector3 hitDirection,
        Vector3 hitPoint,
        bool absorbedByPerfectDodge = false,
        bool absorbedByAssistParry = false,
        int hitStopFrames = 0)
    {
        Context = context;
        TargetTransform = targetTransform;
        HitDirection = hitDirection;
        HitPoint = hitPoint;
        AbsorbedByPerfectDodge = absorbedByPerfectDodge;
        AbsorbedByAssistParry = absorbedByAssistParry;
        HitStopFrames = hitStopFrames > 0 ? hitStopFrames : 0;
    }

    /// <summary>命中上下文，包含招式、Hitbox 与攻击者。</summary>
    public ActionHitContext Context { get; }

    /// <summary>受击目标 Transform；可能为空。</summary>
    public Transform TargetTransform { get; }

    /// <summary>攻击者指向受击者的水平方向，用于镜头震动等反馈。</summary>
    public Vector3 HitDirection { get; }

    /// <summary>受击 Cue 落点（攻击盒中心投影到受击盒）。</summary>
    public Vector3 HitPoint { get; }

    /// <summary>完美闪避吞伤：无受击 Reaction，订阅者勿播受击 Cue。</summary>
    public bool AbsorbedByPerfectDodge { get; }

    /// <summary>招架窗吞伤：勿播打击火花；clang 在 Success Timeline。</summary>
    public bool AbsorbedByAssistParry { get; }

    /// <summary>权威裁定的逻辑卡肉帧；大于 0 时 HitStopController 冻攻击者 VFX。</summary>
    public int HitStopFrames { get; }
}

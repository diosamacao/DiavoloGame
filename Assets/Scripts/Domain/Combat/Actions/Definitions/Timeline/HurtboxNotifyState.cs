using System;
using UnityEngine;

/// <summary>受击框区间窗口；为后续受击部位、弱点、弹刀等编辑器轨道预留统一入口。</summary>
[Serializable]
public class HurtboxNotifyState : ActionNotifyState
{
    [SerializeField] string hurtboxId = "body";
    [SerializeField] HitboxShape shape = HitboxShape.Box;
    [SerializeField] string attachPointId = string.Empty;
    [SerializeField] Vector3 localOffset = Vector3.zero;
    [SerializeField] Vector3 localEulerAngles = Vector3.zero;
    [Tooltip("Box 全尺寸（与 Unity BoxCollider.size 一致），非半长。")]
    [SerializeField] Vector3 size = new(0.8f, 1.8f, 0.8f);

    /// <summary>受击框 id，用于后续部位反应或弱点识别。</summary>
    public string HurtboxId => string.IsNullOrEmpty(hurtboxId) ? "body" : hurtboxId;

    /// <summary>受击框形状；当前预留，运行时常驻 Hurtbox 仍使用 HurtboxDefinition。</summary>
    public HitboxShape Shape => shape;

    /// <summary>未来编辑器解析挂点用的 id。</summary>
    public string AttachPointId => attachPointId;

    /// <summary>相对挂点的局部位置偏移。</summary>
    public Vector3 LocalOffset => localOffset;

    /// <summary>相对挂点的局部欧拉角。</summary>
    public Vector3 LocalEulerAngles => localEulerAngles;

    /// <summary>Box 全尺寸，非半长。</summary>
    public Vector3 Size => size;
}

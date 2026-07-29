using System;
using UnityEngine;

/// <summary>攻击判定框区间窗口；在生效帧内生成相对挂点的攻击 OBB。</summary>
[Serializable]
public class HitboxNotifyState : ActionNotifyState
{
    [SerializeField] HitboxShape shape = HitboxShape.Box;
    [SerializeField] string attachPointId = string.Empty;
    [SerializeField] Vector3 localOffset = new(0f, 1f, 0.8f);
    [SerializeField] Vector3 localEulerAngles = Vector3.zero;
    [Tooltip("Box 全尺寸（与 Unity BoxCollider.size 一致），非半长。")]
    [SerializeField] Vector3 size = new(1.2f, 0.4f, 0.8f);
    [Tooltip("该判定框独立的伤害、受击语义与命中反馈。")]
    [SerializeField] HitPayload payload = new();

    /// <summary>判定形状；当前运行时仅处理 Box。</summary>
    public HitboxShape Shape => shape;

    /// <summary>挂点名；空则使用角色默认挂点，由 CharacterAttachPointResolver 解析。</summary>
    public string AttachPointId => attachPointId;

    /// <summary>相对挂点的局部位置偏移。</summary>
    public Vector3 LocalOffset => localOffset;

    /// <summary>相对挂点的局部欧拉角。</summary>
    public Vector3 LocalEulerAngles => localEulerAngles;

    /// <summary>Box 全尺寸，非半长。</summary>
    public Vector3 Size => size;

    /// <summary>该判定框独立的命中结算载荷。</summary>
    public HitPayload Payload => payload ?? new HitPayload();
}

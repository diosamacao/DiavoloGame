using System;
using UnityEngine;

/// <summary>攻击判定框区间窗口；在生效帧内生成相对挂点的攻击 OBB。</summary>
[Serializable]
public class HitboxNotifyState : ActionNotifyState
{
    [SerializeField] string hitboxId = "weapon_blade";
    [SerializeField] HitboxShape shape = HitboxShape.Box;
    [SerializeField] string attachPointId = string.Empty;
    [SerializeField] Vector3 localOffset = new(0f, 1f, 0.8f);
    [SerializeField] Vector3 localEulerAngles = Vector3.zero;
    [Tooltip("Box 全尺寸（与 Unity BoxCollider.size 一致），非半长。")]
    [SerializeField] Vector3 size = new(1.2f, 0.4f, 0.8f);
    [SerializeField] float damageWeight = 1f;
    [SerializeField] string hitReactionId = string.Empty;

    /// <summary>同招内防重复命中的判定框 id。</summary>
    public string HitboxId => string.IsNullOrEmpty(hitboxId) ? "default" : hitboxId;

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

    /// <summary>伤害倍率预留字段；伤害系统接入前仅作为编辑器数据。</summary>
    public float DamageWeight => Mathf.Max(0f, damageWeight);

    /// <summary>命中反应 id 预留字段；后续受击动作选择使用。</summary>
    public string HitReactionId => hitReactionId;
}

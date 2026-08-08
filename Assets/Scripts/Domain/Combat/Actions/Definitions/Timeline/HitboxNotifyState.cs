using System;
using UnityEngine;

/// <summary>
/// 攻击判定框区间窗口；在生效帧内生成攻击 OBB。
/// parentToAttachPoint：跟随挂点，或在进入窗口时冻结世界空间（对齐 VFX）。
/// </summary>
[Serializable]
public class HitboxNotifyState : ActionNotifyState, ISerializationCallbackReceiver
{
    const byte ParentToAttachPointSchemaVersion = 1;

    [SerializeField] HitboxShape shape = HitboxShape.Box;
    [SerializeField] string attachPointId = string.Empty;
    [SerializeField] Vector3 localOffset = new(0f, 1f, 0.8f);
    [SerializeField] Vector3 localEulerAngles = Vector3.zero;
    [Tooltip("Box 全尺寸（与 Unity BoxCollider.size 一致），非半长。")]
    [SerializeField] Vector3 size = new(1.2f, 0.4f, 0.8f);
    [Tooltip("勾选：每帧跟随挂点/角色根；取消：窗口进入帧写入世界空间后不再跟随。")]
    [SerializeField] bool parentToAttachPoint = true;
    /// <summary>0=旧资产未写入该字段（反序列化 bool 会变 false）；≥1 表示已显式序列化。</summary>
    [SerializeField] byte parentToAttachPointSchema;
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

    /// <summary>是否每帧跟随挂点；false 时在窗口进入帧冻结世界 OBB。</summary>
    public bool ParentToAttachPoint => parentToAttachPoint;

    /// <summary>该判定框独立的命中结算载荷。</summary>
    public HitPayload Payload => payload ?? new HitPayload();

    /// <summary>旧资产缺少字段时 Unity 会把 bool 读成 false；此处纠正为默认跟随。</summary>
    public void OnAfterDeserialize()
    {
        if (parentToAttachPointSchema >= ParentToAttachPointSchemaVersion)
            return;

        parentToAttachPoint = true;
        parentToAttachPointSchema = ParentToAttachPointSchemaVersion;
    }

    /// <summary>写入 schema，避免下次被当成旧资产再强制 true。</summary>
    public void OnBeforeSerialize()
    {
        parentToAttachPointSchema = ParentToAttachPointSchemaVersion;
    }
}

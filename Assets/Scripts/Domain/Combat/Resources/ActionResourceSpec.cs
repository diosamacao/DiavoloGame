using System;
using UnityEngine;

/// <summary>
/// 挂在 ActionDefinition 上的静态资源价签；Gate/Pipeline 只读本结构，禁止散落顶层 cost 字段。
/// </summary>
[Serializable]
public sealed class ActionResourceSpec
{
    [Tooltip("资源/技能槽标签（可与 Graph 路由配合）。")]
    [SerializeField] ActionResourceTag resourceTag = ActionResourceTag.None;

    [Tooltip("起手/切招消耗的能量（整数点）。")]
    [SerializeField] int energyCost;

    [Tooltip("ConfirmHit 后回填能量；EX/Ult 通常填 0。")]
    [SerializeField] int energyGrantOnHit;

    [Tooltip("ConfirmHit 后回填喧响。")]
    [SerializeField] int decibelGrantOnHit;

    [Tooltip("起手是否消耗 1 次闪避充能。")]
    [SerializeField] bool consumeDodgeCharge;

    [Tooltip("起手要求喧响已满。")]
    [SerializeField] bool requiresDecibelFull;

    [Tooltip("起手清空喧响（终结技）。")]
    [SerializeField] bool clearsDecibelOnStart;

    /// <summary>资源标签。</summary>
    public ActionResourceTag ResourceTag => resourceTag;

    /// <summary>能量消耗。</summary>
    public int EnergyCost => Mathf.Max(0, energyCost);

    /// <summary>命中回能。</summary>
    public int EnergyGrantOnHit => Mathf.Max(0, energyGrantOnHit);

    /// <summary>命中回喧响。</summary>
    public int DecibelGrantOnHit => Mathf.Max(0, decibelGrantOnHit);

    /// <summary>是否耗闪避次数。</summary>
    public bool ConsumeDodgeCharge => consumeDodgeCharge;

    /// <summary>是否要求喧响满。</summary>
    public bool RequiresDecibelFull => requiresDecibelFull;

    /// <summary>是否起手清喧响。</summary>
    public bool ClearsDecibelOnStart => clearsDecibelOnStart;

    /// <summary>空价签（无消耗无回填）。</summary>
    public static ActionResourceSpec Empty { get; } = new();

    /// <summary>是否声明了任何起手消耗或门槛。</summary>
    public bool HasStartupCost =>
        EnergyCost > 0 || ConsumeDodgeCharge || RequiresDecibelFull || ClearsDecibelOnStart;

    /// <summary>供 EditMode 测试与工具构造价签；运行时内容仍以资产序列化为准。</summary>
    public static ActionResourceSpec Create(
        ActionResourceTag tag = ActionResourceTag.None,
        int energyCost = 0,
        int energyGrantOnHit = 0,
        int decibelGrantOnHit = 0,
        bool consumeDodgeCharge = false,
        bool requiresDecibelFull = false,
        bool clearsDecibelOnStart = false)
    {
        return new ActionResourceSpec
        {
            resourceTag = tag,
            energyCost = energyCost,
            energyGrantOnHit = energyGrantOnHit,
            decibelGrantOnHit = decibelGrantOnHit,
            consumeDodgeCharge = consumeDodgeCharge,
            requiresDecibelFull = requiresDecibelFull,
            clearsDecibelOnStart = clearsDecibelOnStart,
        };
    }
}

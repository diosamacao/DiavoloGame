using System;
using UnityEngine;

/// <summary>角色战斗侧装配参数，避免索敌和判定挂点散落在多个组件字段。</summary>
[Serializable]
public struct CharacterCombatConfig
{
    [SerializeField] int teamId;
    [SerializeField] string attachPointName;
    [SerializeField] HurtboxDefinition hurtbox;
    [SerializeField] float maxHealth;
    [Tooltip("唯一 SelectedTarget 的自动选中/显式切换范围（米）。")]
    [SerializeField] float targetAcquireRangeMeters;
    [Tooltip("当前 SelectedTarget 的保持范围（米），须不小于选中范围。")]
    [SerializeField] float targetRetainRangeMeters;
    [Tooltip("上层控制器用于选择受击与死亡表现动作的规则集。")]
    [SerializeField] CharacterReactionSet reactions;
    [Tooltip("站立韧性。杂兵 1，精英 3，Boss 5。禁止按身份 if。")]
    [SerializeField] int baseInterruptResist;

    /// <summary>默认玩家阵营与空挂点名。</summary>
    public static CharacterCombatConfig Default => new()
    {
        teamId = 0,
        attachPointName = string.Empty,
        hurtbox = new HurtboxDefinition(),
        maxHealth = 100f,
        targetAcquireRangeMeters = 12f,
        targetRetainRangeMeters = 13.5f,
        reactions = new CharacterReactionSet(),
        baseInterruptResist = HitReactionResolveQuery.DefaultBaseInterruptResist,
    };

    /// <summary>攻击者阵营 id；索敌会排除同阵营目标。</summary>
    public int TeamId => teamId;

    /// <summary>Hitbox/VFX 默认挂点名；为空时使用角色根。</summary>
    public string AttachPointName => attachPointName;

    /// <summary>角色根节点上的默认受击框；旧配置缺失时使用标准人形 Box。</summary>
    public HurtboxDefinition Hurtbox => hurtbox ?? new HurtboxDefinition();

    /// <summary>玩家等未被上层 Definition 覆盖时使用的最大生命值。</summary>
    public float MaxHealth => maxHealth > 0f ? maxHealth : 100f;

    /// <summary>自动选中与 TargetSwitch 可使用的范围；旧资产缺失字段时回退 12 米。</summary>
    public float TargetAcquireRangeMeters =>
        targetAcquireRangeMeters > 0f ? targetAcquireRangeMeters : 12f;

    /// <summary>已选目标保持范围；旧资产或非法值时回退为 Acquire + 1.5 米。</summary>
    public float TargetRetainRangeMeters =>
        targetRetainRangeMeters >= TargetAcquireRangeMeters
            ? targetRetainRangeMeters
            : TargetAcquireRangeMeters + 1.5f;

    /// <summary>供玩家或敌人上层控制器解析受击、死亡表现的规则集。</summary>
    public CharacterReactionSet Reactions => reactions ?? new CharacterReactionSet();

    /// <summary>站立韧性；旧资产未填时按杂兵 1。</summary>
    public int BaseInterruptResist =>
        baseInterruptResist > 0
            ? baseInterruptResist
            : HitReactionResolveQuery.DefaultBaseInterruptResist;

    /// <summary>把未填的韧性写成杂兵默认；调用方须写回 struct。</summary>
    public void EnsureInterruptResistDefault()
    {
        if (baseInterruptResist <= 0)
            baseInterruptResist = HitReactionResolveQuery.DefaultBaseInterruptResist;
    }
}

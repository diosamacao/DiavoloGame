/// <summary>
/// 裁定输入。打断等级与期望档由调用方填好；旧盒子用默认 LightStun + level 1。
/// HitPayload 资产字段仍待 P-HR3 写入，本结构先作为唯一真源。
/// </summary>
public readonly struct HitReactionResolveQuery
{
    /// <summary>旧 Hitbox 未填打断等级时的缺省值。</summary>
    public const int DefaultInterruptLevel = 1;

    /// <summary>未配置抗打断时按杂兵站立抗性。</summary>
    public const int DefaultBaseInterruptResist = 1;

    /// <summary>组装一次完整裁定查询；调用方负责填默认值。</summary>
    public HitReactionResolveQuery(
        bool isDead,
        bool isFatal,
        bool isInvincible,
        bool absorbedByPerfectDodge,
        bool isDot,
        bool hasHitPayload,
        int interruptLevel,
        HitReactionKind desiredReaction,
        int baseInterruptResist,
        int phaseInterruptResistBonus,
        bool superArmor,
        string hitReactionId)
    {
        IsDead = isDead;
        IsFatal = isFatal;
        IsInvincible = isInvincible;
        AbsorbedByPerfectDodge = absorbedByPerfectDodge;
        IsDot = isDot;
        HasHitPayload = hasHitPayload;
        InterruptLevel = interruptLevel;
        DesiredReaction = desiredReaction;
        BaseInterruptResist = baseInterruptResist;
        PhaseInterruptResistBonus = phaseInterruptResistBonus;
        SuperArmor = superArmor;
        HitReactionId = hitReactionId ?? string.Empty;
    }

    /// <summary>目标生命已归零。</summary>
    public bool IsDead { get; }

    /// <summary>本帧扣血后将致死。</summary>
    public bool IsFatal { get; }

    /// <summary>无敌窗；管道通常早退，Resolver 仍返回 None。</summary>
    public bool IsInvincible { get; }

    /// <summary>完美闪避吞伤。</summary>
    public bool AbsorbedByPerfectDodge { get; }

    /// <summary>周期 DOT / 无打击语义的数值伤。</summary>
    public bool IsDot { get; }

    /// <summary>是否来自 Hitbox Payload。无 Payload 的数值伤视为 None。</summary>
    public bool HasHitPayload { get; }

    /// <summary>本刀打断等级。</summary>
    public int InterruptLevel { get; }

    /// <summary>攻击期望档；未打断成功时最多落到 Flinch。</summary>
    public HitReactionKind DesiredReaction { get; }

    /// <summary>站立抗打断。</summary>
    public int BaseInterruptResist { get; }

    /// <summary>当前 Phase 抗打断加成。</summary>
    public int PhaseInterruptResistBonus { get; }

    /// <summary>SuperArmor 窗：非 Death 不可打断，最多 Flinch。</summary>
    public bool SuperArmor { get; }

    /// <summary>Stun+ 选受击 Action 的语义 Id。</summary>
    public string HitReactionId { get; }

    /// <summary>
    /// 常规打击查询。空字段默认 level=1、desired=LightStun，与未改旧盒子手感接近。
    /// </summary>
    public static HitReactionResolveQuery CombatHit(
        int interruptLevel = DefaultInterruptLevel,
        HitReactionKind desiredReaction = HitReactionKind.LightStun,
        int baseInterruptResist = DefaultBaseInterruptResist,
        int phaseInterruptResistBonus = 0,
        bool superArmor = false,
        string hitReactionId = "")
    {
        return new HitReactionResolveQuery(
            isDead: false,
            isFatal: false,
            isInvincible: false,
            absorbedByPerfectDodge: false,
            isDot: false,
            hasHitPayload: true,
            interruptLevel,
            desiredReaction,
            baseInterruptResist,
            phaseInterruptResistBonus,
            superArmor,
            hitReactionId);
    }

    /// <summary>
    /// 从命中上下文取 ReactionId；打断字段尚未进 Payload，一律用旧盒子默认。
    /// Hitbox / Payload 皆空视为无打击语义。
    /// </summary>
    public static HitReactionResolveQuery FromHitContext(
        in ActionHitContext context,
        bool isDead = false,
        bool isFatal = false,
        bool isInvincible = false,
        bool absorbedByPerfectDodge = false,
        bool isDot = false,
        int baseInterruptResist = DefaultBaseInterruptResist,
        int phaseInterruptResistBonus = 0,
        bool superArmor = false)
    {
        HitPayload payload = context.Hitbox != null ? context.Hitbox.Payload : null;
        return new HitReactionResolveQuery(
            isDead,
            isFatal,
            isInvincible,
            absorbedByPerfectDodge,
            isDot,
            hasHitPayload: payload != null,
            DefaultInterruptLevel,
            HitReactionKind.LightStun,
            baseInterruptResist,
            phaseInterruptResistBonus,
            superArmor,
            payload != null ? payload.HitReactionId : string.Empty);
    }
}

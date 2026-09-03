/// <summary>
/// 裁定输入。冲击力与韧性由调用方填好；旧盒子默认冲击 1、韧性 1。
/// </summary>
public readonly struct HitReactionResolveQuery
{
    /// <summary>旧 Hitbox 未填冲击力时的缺省值。</summary>
    public const int DefaultInterruptLevel = 1;

    /// <summary>未配置韧性时按杂兵站立韧性。</summary>
    public const int DefaultBaseInterruptResist = 1;

    /// <summary>冲击力 − 韧性 ≥ 此值升为 HeavyStun。</summary>
    public const int HeavyStunExcess = 2;

    /// <summary>冲击力 − 韧性 ≥ 此值升为 Launch。</summary>
    public const int LaunchExcess = 4;

    /// <summary>组装一次完整裁定查询；调用方负责填默认值。</summary>
    public HitReactionResolveQuery(
        bool isDead,
        bool isFatal,
        bool isInvincible,
        bool absorbedByPerfectDodge,
        bool isDot,
        bool hasHitPayload,
        int interruptLevel,
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

    /// <summary>本刀冲击力（序列化字段 interruptLevel）。</summary>
    public int InterruptLevel { get; }

    /// <summary>站立韧性（序列化字段 baseInterruptResist）。</summary>
    public int BaseInterruptResist { get; }

    /// <summary>当前 Phase 韧性加成。</summary>
    public int PhaseInterruptResistBonus { get; }

    /// <summary>SuperArmor 窗：非 Death 最多 Flinch。</summary>
    public bool SuperArmor { get; }

    /// <summary>Stun+ 选受击 Action 的语义 Id。</summary>
    public string HitReactionId { get; }

    /// <summary>本刀冲击力，等同 InterruptLevel。</summary>
    public int Impact => InterruptLevel;

    /// <summary>受击方当前韧性 = 站立 + Phase 加成，负值按 0。</summary>
    public int Toughness
    {
        get
        {
            int stand = BaseInterruptResist < 0 ? 0 : BaseInterruptResist;
            int bonus = PhaseInterruptResistBonus < 0 ? 0 : PhaseInterruptResistBonus;
            return stand + bonus;
        }
    }

    /// <summary>常规打击查询。空字段默认冲击 1、韧性 1。</summary>
    public static HitReactionResolveQuery CombatHit(
        int interruptLevel = DefaultInterruptLevel,
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
            baseInterruptResist,
            phaseInterruptResistBonus,
            superArmor,
            hitReactionId);
    }

    /// <summary>从命中上下文读取冲击力；无 Hitbox 视为无打击语义。</summary>
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
            payload != null ? payload.InterruptLevel : DefaultInterruptLevel,
            baseInterruptResist,
            phaseInterruptResistBonus,
            superArmor,
            payload != null ? payload.HitReactionId : string.Empty);
    }
}

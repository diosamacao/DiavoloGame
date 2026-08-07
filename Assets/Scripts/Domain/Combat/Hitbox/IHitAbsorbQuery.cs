/// <summary>命中结算前查询目标是否应吞伤（无敌 / 完美闪避）。</summary>
public interface IHitAbsorbQuery
{
    /// <summary>普通 i-frame：吞伤、不 Grant、不武装完美反击。</summary>
    bool IsInvincible { get; }

    /// <summary>完美闪避窗：吞伤、不 Grant、武装反击缓冲（优先于无敌语义）。</summary>
    bool IsInPerfectDodgeWindow { get; }
}

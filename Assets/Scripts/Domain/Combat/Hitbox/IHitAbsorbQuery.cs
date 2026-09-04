/// <summary>命中结算前查询目标是否应吞伤（招架窗 / 完美闪避 / 无敌）。</summary>
public interface IHitAbsorbQuery
{
    /// <summary>普通 i-frame：吞伤、不 Grant、不武装完美反击。</summary>
    bool IsInvincible { get; }

    /// <summary>完美闪避窗：吞伤、不 Grant、武装反击缓冲（优先于无敌语义）。</summary>
    bool IsInPerfectDodgeWindow { get; }

    /// <summary>招架接触窗：吞伤、不 Grant、对攻击者 IssueParried（优先于完美闪避与无敌）。</summary>
    bool IsInAssistParryWindow { get; }
}

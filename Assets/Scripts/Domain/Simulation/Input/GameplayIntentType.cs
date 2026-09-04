/// <summary>设备无关的离散玩法意图；动作图只按此语义键选招。</summary>
public enum GameplayIntentType
{
    /// <summary>未配置，不参与动作解析。</summary>
    None = 0,

    /// <summary>普通攻击按下。</summary>
    Attack = 1,

    /// <summary>攻击键达到长按阈值。</summary>
    LongPressedAttack = 2,

    /// <summary>Sprint 稳态下按下攻击。</summary>
    SprintAttack = 3,

    /// <summary>闪避按下。</summary>
    Dodge = 4,

    /// <summary>攻击键松开；蓄力释放等招式的 Trigger。</summary>
    AttackRelease = 5,

    /// <summary>
    /// 特殊技同键意图（原 Skill=6）；Producer 不区分 EX，由 Graph+Gate 按能量分支。
    /// </summary>
    Special = 6,

    /// <summary>闪避 Action 播放期间按下攻击。</summary>
    DodgeAttack = 7,

    /// <summary>终结技意图；需喧响满档（RequiresDecibelFull）。</summary>
    Ultimate = 8,

    /// <summary>
    /// 完美闪避反击专用意图；仅在 HasPerfectDodgeCounter 时由 Producer 派生。
    /// Graph Entry 指向 Counter，禁止挂在 Attack 上隐式换招。
    /// </summary>
    PerfectDodgeAttack = 9,

    /// <summary>普通切人时由座位协调器注入，上场角色 Graph Entry 播放登场动作。</summary>
    SwitchIn = 10,

    /// <summary>普通退场时由座位协调器注入；角色在该动作首次进入 Recovery 后隐藏。</summary>
    SwitchOut = 11,

    /// <summary>金光近战极限支援：上场 Guard（举刀），不由攻击键产生。</summary>
    AssistParry = 12,

    /// <summary>敌人 Hitbox 接触 Guard 窗后切成功段；不由切人键产生。</summary>
    AssistParrySuccess = 13,

    /// <summary>金光远程极限支援：上场回避 Guard。</summary>
    AssistEvade = 14,

    /// <summary>红光或降级：换人极限闪避。</summary>
    SwitchPerfectDodge = 15,

    /// <summary>接触成功后武装，攻击族按键派生支援突击。</summary>
    AssistFollowUp = 16,
}

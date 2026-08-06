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
}

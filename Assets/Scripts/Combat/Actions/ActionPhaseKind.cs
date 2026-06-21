/// <summary>动作阶段语义；Invincible / SuperArmor 为覆盖标记，可与三相区间重叠。</summary>
public enum ActionPhaseKind
{
    Startup = 0,
    Active = 1,
    Recovery = 2,

    /// <summary>无敌帧覆盖标记（I-Frame）。</summary>
    Invincible = 10,

    /// <summary>霸体覆盖标记。</summary>
    SuperArmor = 11,
}

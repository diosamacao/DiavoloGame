/// <summary>连段队列末段再次输入时的行为。</summary>
public enum ComboLeafPolicy
{
    /// <summary>回到队列首段，继续循环连段。</summary>
    LoopToRoot = 0,

    /// <summary>不再衔接，Cancel 不生效。</summary>
    StopCombo = 1,
}

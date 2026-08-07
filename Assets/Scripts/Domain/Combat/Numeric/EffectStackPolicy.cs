/// <summary>同 Id Effect 再施加时的叠层策略。</summary>
public enum EffectStackPolicy : byte
{
    /// <summary>移除旧实例，重新施加。</summary>
    Replace = 0,

    /// <summary>刷新剩余持续时间，不增加层数。</summary>
    Refresh = 1,

    /// <summary>层数 +1（达 maxStacks 后不再加层，仍刷新持续时间）。</summary>
    StackCount = 2,
}

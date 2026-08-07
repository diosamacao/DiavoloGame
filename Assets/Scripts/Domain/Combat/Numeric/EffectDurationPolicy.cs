/// <summary>Effect 持续时间策略（GAS-lite 白名单）。</summary>
public enum EffectDurationPolicy : byte
{
    /// <summary>立即改 Base，不进入 Active 列表。</summary>
    Instant = 0,

    /// <summary>持续修饰，到期移除 Modifier。</summary>
    Duration = 1,

    /// <summary>按间隔跳改 Base（DOT/HOT）；不做被动回能。</summary>
    Periodic = 2,
}

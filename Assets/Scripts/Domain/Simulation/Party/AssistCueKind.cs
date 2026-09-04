/// <summary>敌人进攻 Timeline 上的极限支援闪光种类。</summary>
public enum AssistCueKind : byte
{
    /// <summary>金光：点数够且类型匹配时切招架/回避。</summary>
    Gold = 0,

    /// <summary>红光或降级：切人走换人闪，不耗支援点。</summary>
    Red = 1,
}

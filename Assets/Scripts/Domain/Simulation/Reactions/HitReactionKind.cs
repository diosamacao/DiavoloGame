/// <summary>
/// 受击裁定档。Flinch 及以下不断招；LightStun 及以上进 Hit。
/// 位于 Simulation 程序集，供复制命中事件与 Domain 裁定共用。
/// </summary>
public enum HitReactionKind : byte
{
    /// <summary>无动作反馈；伤害仍可已结算。</summary>
    None = 0,

    /// <summary>微颤：不停 ActionSim，表现叠 Additive。</summary>
    Flinch = 1,

    /// <summary>轻击退：进 Hit 并停招。</summary>
    LightStun = 2,

    /// <summary>重击退：进 Hit 并停招。</summary>
    HeavyStun = 3,

    /// <summary>击飞：进 Hit 并停招；位移仍用现有受击 Action。</summary>
    Launch = 4,

    /// <summary>死亡：进 Death。</summary>
    Death = 5,
}

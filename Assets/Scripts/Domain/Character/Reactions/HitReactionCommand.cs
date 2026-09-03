/// <summary>
/// 受击裁定结果。Resolver 只填本结构，不切状态、不播动画。
/// </summary>
public readonly struct HitReactionCommand
{
    /// <summary>无反馈；伤害若已结算则保持。</summary>
    public static HitReactionCommand None { get; } = new(
        HitReactionKind.None,
        stunAction: null,
        stunFrames: 0,
        AnimationKey.Idle);

    /// <summary>创建已裁定的反馈命令。</summary>
    public HitReactionCommand(
        HitReactionKind kind,
        ActionDefinition stunAction,
        int stunFrames,
        AnimationKey flinchKey)
    {
        Kind = kind;
        StunAction = stunAction;
        StunFrames = stunFrames > 0 ? stunFrames : 0;
        FlinchKey = flinchKey;
    }

    /// <summary>本刀最终档。</summary>
    public HitReactionKind Kind { get; }

    /// <summary>Stun+ / Death 选用的表现 Action；Flinch / None 为空。</summary>
    public ActionDefinition StunAction { get; }

    /// <summary>无受击 Action 时 HitState 使用的逻辑帧数。</summary>
    public int StunFrames { get; }

    /// <summary>仅 Flinch 使用的 Additive 逻辑键。</summary>
    public AnimationKey FlinchKey { get; }

    /// <summary>LightStun 及以上才停招、进 Hit / Death。</summary>
    public bool InterruptsAction => Kind >= HitReactionKind.LightStun;

    /// <summary>同帧多刀取更高档；同档保留先到的一条，避免既 Flinch 又 EnterHit。</summary>
    public static HitReactionCommand Merge(in HitReactionCommand left, in HitReactionCommand right) =>
        right.Kind > left.Kind ? right : left;

    /// <summary>按声明顺序折叠多刀；空数组视为 None。</summary>
    public static HitReactionCommand Merge(params HitReactionCommand[] commands)
    {
        if (commands == null || commands.Length == 0)
            return None;

        HitReactionCommand best = commands[0];
        for (int i = 1; i < commands.Length; i++)
            best = Merge(in best, in commands[i]);
        return best;
    }
}

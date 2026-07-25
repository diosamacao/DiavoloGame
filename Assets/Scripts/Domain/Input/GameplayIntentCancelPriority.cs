/// <summary>
/// 同一 Cancel 路由多意图同时缓冲时的解析优先级，以及清缓冲时的保留策略。
/// 数值越大越优先，避免连段 Attack 边在 HashSet 遍历顺序下永远抢赢蓄力。
/// </summary>
public static class GameplayIntentCancelPriority
{
    /// <summary>返回意图在 Cancel 候选竞争中的优先级。</summary>
    public static int Get(GameplayIntentType intent)
    {
        switch (intent)
        {
            case GameplayIntentType.Dodge:
                return 100;
            case GameplayIntentType.DodgeAttack:
                return 90;
            case GameplayIntentType.AttackRelease:
                return 80;
            case GameplayIntentType.LongPressedAttack:
                return 50;
            case GameplayIntentType.SprintAttack:
                return 40;
            case GameplayIntentType.Attack:
                return 10;
            default:
                return 0;
        }
    }

    /// <summary>
    /// 消费 keepIntent 后是否应保留 candidate 缓冲。
    /// 连段消费 Attack 时保留 LongPressedAttack，避免同一按住周期内蓄力意图被清掉且无法重发。
    /// </summary>
    public static bool ShouldRetainAfterConsume(
        GameplayIntentType keepIntent,
        GameplayIntentType candidate)
    {
        if (candidate == keepIntent || candidate == GameplayIntentType.None)
            return true;

        // 仅保留「已确认的长按蓄力」跨连段 Cancel；不保留 AttackRelease（防进蓄力瞬间秒放）。
        return candidate == GameplayIntentType.LongPressedAttack
            && (keepIntent == GameplayIntentType.Attack
                || keepIntent == GameplayIntentType.SprintAttack);
    }
}

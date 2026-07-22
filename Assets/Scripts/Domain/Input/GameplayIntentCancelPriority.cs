/// <summary>
/// Cancel 同槽多意图同时缓冲时的解析优先级。
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
}

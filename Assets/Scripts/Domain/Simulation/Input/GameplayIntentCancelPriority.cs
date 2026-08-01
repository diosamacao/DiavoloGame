/// <summary>定义多意图取消竞争优先级及消费后的缓冲保留策略。</summary>
public static class GameplayIntentCancelPriority
{
    /// <summary>返回意图在取消候选竞争中的优先级，数值越大越优先。</summary>
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

    /// <summary>返回消费 keepIntent 后是否应保留 candidate 缓冲。</summary>
    public static bool ShouldRetainAfterConsume(
        GameplayIntentType keepIntent,
        GameplayIntentType candidate)
    {
        if (candidate == keepIntent || candidate == GameplayIntentType.None)
            return true;

        // 长按确认只跨普通攻击类 Cancel 保留，避免进入蓄力动作时丢失已达阈值的意图。
        return candidate == GameplayIntentType.LongPressedAttack
            && (keepIntent == GameplayIntentType.Attack
                || keepIntent == GameplayIntentType.SprintAttack);
    }
}

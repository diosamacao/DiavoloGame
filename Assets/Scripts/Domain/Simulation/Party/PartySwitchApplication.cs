/// <summary>把换人 kind 映射为上场 Graph Intent；InstantReplace 不播 SwitchIn/Out。</summary>
public static class PartySwitchApplication
{
    /// <summary>上场应注入的外部意图。</summary>
    public static GameplayIntentType ToIncomingIntent(PartySwitchKind kind)
    {
        switch (kind)
        {
            case PartySwitchKind.AssistParry:
                return GameplayIntentType.AssistParry;
            case PartySwitchKind.AssistEvade:
                return GameplayIntentType.AssistEvade;
            case PartySwitchKind.SwitchPerfectDodge:
                return GameplayIntentType.SwitchPerfectDodge;
            default:
                return GameplayIntentType.SwitchIn;
        }
    }

    /// <summary>回避支援用 Cue 的 evade 偏移；招架 / 换人闪用 parry 偏移。</summary>
    public static bool UsesEvadeOffset(PartySwitchKind kind) =>
        kind == PartySwitchKind.AssistEvade;
}

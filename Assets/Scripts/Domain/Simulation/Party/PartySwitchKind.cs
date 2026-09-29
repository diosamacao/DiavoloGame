/// <summary>换人命令对应的上场招式类别。</summary>
public enum PartySwitchKind
{
    /// <summary>无支援窗口时播放普通登场动作。</summary>
    SwitchIn = 0,

    /// <summary>金光近战：上场 AssistParry Guard。</summary>
    AssistParry = 1,

    /// <summary>金光远程：上场 AssistEvade。</summary>
    AssistEvade = 2,

    /// <summary>红光或降级：上场 SwitchPerfectDodge。</summary>
    SwitchPerfectDodge = 3,
}

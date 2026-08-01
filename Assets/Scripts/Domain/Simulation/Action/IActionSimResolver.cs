using System.Collections.Generic;

/// <summary>把当前输入意图解析为取消或 Recovery 入口动作。</summary>
public interface IActionSimResolver
{
    /// <summary>枚举当前动作图可消费的全部玩法意图。</summary>
    IEnumerable<GameplayIntentType> EnumerateActiveIntents();

    /// <summary>解析当前图节点在指定取消窗口上的下一动作。</summary>
    bool TryResolveNext(
        GameplayIntentType intent,
        CancelWindowType windowType,
        in ActionSimSnapshot snapshot,
        out ActionSimResolveResult result);

    /// <summary>解析 Recovery 阶段从当前图入口重新起手的动作。</summary>
    bool TryResolveRecoveryStart(
        GameplayIntentType intent,
        in ActionSimSnapshot snapshot,
        out ActionSimResolveResult result);
}

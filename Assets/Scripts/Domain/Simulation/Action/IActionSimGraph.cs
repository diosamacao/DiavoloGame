using System.Collections.Generic;

/// <summary>向 ActionSim 提供取消路由与无输入自动衔接的纯图查询。</summary>
public interface IActionSimGraph
{
    /// <summary>收集当前节点在指定取消窗口上的候选意图并写入去重集合。</summary>
    void CollectCancelCandidateIntents(
        string nodeId,
        CancelWindowType windowType,
        ISet<GameplayIntentType> results);

    /// <summary>解析当前节点的自动衔接；空目标终边通过 shouldStop 请求自然停止。</summary>
    bool TryResolveAutomaticTransition(
        string nodeId,
        IActionSimContent content,
        int currentFrame,
        bool hasConfirmedHit,
        out ActionSimResolveResult result,
        out bool shouldStop);
}

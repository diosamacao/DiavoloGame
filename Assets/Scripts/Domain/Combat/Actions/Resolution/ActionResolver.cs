using UnityEngine;

/// <summary>动作解析策略基类：把一次 ActionRequest + 上下文解析为最终要播放的 ActionDefinition。</summary>
public abstract class ActionResolver : ScriptableObject
{
    /// <summary>解析最终动作；无法解析（缺配置或方向不满足）时返回 false 且不产生动作。</summary>
    public abstract bool TryResolve(
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionDefinition action);
}

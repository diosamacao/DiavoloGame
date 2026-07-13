using UnityEngine;

/// <summary>动作解析策略基类：把一次 ActionRequest + 上下文解析为 ActionResolveResult（含可选图游标）。</summary>
public abstract class ActionResolver : ScriptableObject
{
    /// <summary>解析最终动作；无法解析（缺配置或方向不满足）时返回 false。</summary>
    public abstract bool TryResolve(
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionResolveResult result);
}

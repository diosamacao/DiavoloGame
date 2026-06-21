using System.Collections.Generic;

/// <summary>运行时目标注册表，集中服务索敌与命中检测，避免每个系统维护自己的全局列表。</summary>
public static class TargetRegistry
{
    static readonly List<IHurtboxTarget> s_targets = new();

    /// <summary>当前已注册的全部受击目标。</summary>
    public static IReadOnlyList<IHurtboxTarget> ActiveTargets => s_targets;

    /// <summary>目标启用时注册。</summary>
    public static void Register(IHurtboxTarget target)
    {
        if (target != null && !s_targets.Contains(target))
            s_targets.Add(target);
    }

    /// <summary>目标禁用时注销。</summary>
    public static void Unregister(IHurtboxTarget target)
    {
        if (target != null)
            s_targets.Remove(target);
    }
}

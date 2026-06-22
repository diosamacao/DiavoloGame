using System.Collections.Generic;

/// <summary>架构级目标注册系统；统一维护可命中与可索敌目标列表。</summary>
public sealed class TargetSystem : IArchitectureSystem
{
    readonly List<IHurtboxTarget> _targets = new();

    /// <summary>当前已注册的全部受击目标。</summary>
    public IReadOnlyList<IHurtboxTarget> ActiveTargets => _targets;

    /// <summary>初始化目标系统。</summary>
    public void Initialize(ACTGameArchitecture architecture) { }

    /// <summary>目标启用时注册。</summary>
    public void Register(IHurtboxTarget target)
    {
        if (target != null && !_targets.Contains(target))
            _targets.Add(target);
    }

    /// <summary>目标禁用时注销。</summary>
    public void Unregister(IHurtboxTarget target)
    {
        if (target != null)
            _targets.Remove(target);
    }
}

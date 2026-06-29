using System.Collections.Generic;
using UnityEngine;

/// <summary>架构级战斗角色注册系统；统一维护角色 Actor、动作执行器与 Animator 查询。</summary>
public sealed class CombatActorSystem : ArchitectureSystemBase
{
    readonly Dictionary<Transform, CombatActorEntry> _entries = new();

    /// <summary>初始化角色注册系统；当前系统无额外启动逻辑。</summary>
    protected override void OnInit() { }

    /// <summary>注册一个战斗角色实例及其动作执行器。</summary>
    public void Register(
        Transform root,
        CharacterActor actor,
        ActionExecutor actionExecutor,
        Animator animator)
    {
        if (root == null)
            return;

        _entries[root] = new CombatActorEntry(actor, actionExecutor, animator);
    }

    /// <summary>注销一个战斗角色实例。</summary>
    public void Unregister(Transform root)
    {
        if (root != null)
            _entries.Remove(root);
    }

    /// <summary>按角色根节点查询战斗角色条目。</summary>
    public bool TryGet(Transform root, out CombatActorEntry entry)
    {
        if (root != null && _entries.TryGetValue(root, out entry))
            return true;

        entry = default;
        return false;
    }
}

/// <summary>战斗角色注册条目，集中暴露角色实例、动作执行器和 Animator。</summary>
public readonly struct CombatActorEntry
{
    /// <summary>创建战斗角色注册条目。</summary>
    public CombatActorEntry(CharacterActor actor, ActionExecutor actionExecutor, Animator animator)
    {
        Actor = actor;
        ActionExecutor = actionExecutor;
        Animator = animator;
    }

    /// <summary>单角色运行实例。</summary>
    public CharacterActor Actor { get; }

    /// <summary>单角色动作执行器。</summary>
    public ActionExecutor ActionExecutor { get; }

    /// <summary>角色 Animator，用于卡肉等表现冻结。</summary>
    public Animator Animator { get; }
}

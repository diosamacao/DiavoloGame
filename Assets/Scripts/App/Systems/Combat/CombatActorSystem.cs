using System.Collections.Generic;
using UnityEngine;

/// <summary>架构级战斗角色注册系统；统一维护角色 Actor 与动画门面查询。</summary>
public sealed class CombatActorSystem : ArchitectureSystemBase
{
    readonly Dictionary<Transform, CombatActorEntry> _entries = new();

    /// <summary>初始化角色注册系统；当前系统无额外启动逻辑。</summary>
    protected override void OnInit() { }

    /// <summary>注册一个战斗角色实例及其动画门面。</summary>
    public void Register(
        Transform root,
        CharacterActor actor,
        CharacterAnimationService animation)
    {
        if (root == null)
            return;

        _entries[root] = new CombatActorEntry(actor, animation);
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

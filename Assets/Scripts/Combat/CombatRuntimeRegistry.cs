using System.Collections.Generic;
using UnityEngine;

/// <summary>角色战斗运行时注册表，供场景级反馈系统按攻击者 Transform 查询纯 C# runtime。</summary>
public static class CombatRuntimeRegistry
{
    static readonly Dictionary<Transform, Entry> s_entries = new();

    /// <summary>注册角色战斗运行时。</summary>
    public static void Register(
        Transform root,
        ActionRuntimeController actionRuntime,
        Animator animator)
    {
        if (root == null)
            return;

        s_entries[root] = new Entry(actionRuntime, animator);
    }

    /// <summary>注销角色战斗运行时。</summary>
    public static void Unregister(Transform root)
    {
        if (root != null)
            s_entries.Remove(root);
    }

    /// <summary>查找攻击者对应的动作运行时和 Animator。</summary>
    public static bool TryGet(Transform root, out ActionRuntimeController actionRuntime, out Animator animator)
    {
        if (root != null && s_entries.TryGetValue(root, out Entry entry))
        {
            actionRuntime = entry.ActionRuntime;
            animator = entry.Animator;
            return true;
        }

        actionRuntime = null;
        animator = null;
        return false;
    }

    readonly struct Entry
    {
        public Entry(ActionRuntimeController actionRuntime, Animator animator)
        {
            ActionRuntime = actionRuntime;
            Animator = animator;
        }

        public ActionRuntimeController ActionRuntime { get; }
        public Animator Animator { get; }
    }
}

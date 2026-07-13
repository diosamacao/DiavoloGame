using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 角色出招表：绑定一张 ActionGraph（可含多个起手 Entry：攻击/闪避等）。
/// 离散输入不再在本表重复配置，一律来自图中 ActionDefinition.Trigger。
/// </summary>
[CreateAssetMenu(fileName = "PlayerActionSet", menuName = "ACT/Combat/Player Action Set")]
public class PlayerActionSet : ScriptableObject
{
    [Tooltip("本模式的连招/起手图；Entry 节点按 Trigger 区分 Attack、Dodge 等。")]
    [SerializeField] ActionGraph actionGraph;

    /// <summary>绑定的动作图。</summary>
    public ActionGraph ActionGraph => actionGraph;

    /// <summary>已绑定有效 ActionGraph。</summary>
    public bool IsValid => actionGraph != null;

    /// <summary>收集图中全部 Trigger 的 InputActionReference（去重），供 InputReader 注册。</summary>
    public InputActionReference[] CollectTriggerInputReferences()
    {
        if (actionGraph == null)
            return Array.Empty<InputActionReference>();

        var list = new List<InputActionReference>(8);
        actionGraph.CollectTriggerInputReferences(list);
        return InputBindingUtils.CollectUniqueReferences(list);
    }

    /// <summary>枚举图中全部 Trigger inputId。</summary>
    public IEnumerable<string> EnumerateTriggerInputIds()
    {
        if (actionGraph == null)
            yield break;

        var set = new HashSet<string>(StringComparer.Ordinal);
        actionGraph.CollectTriggerInputIds(set);
        foreach (string id in set)
            yield return id;
    }
}

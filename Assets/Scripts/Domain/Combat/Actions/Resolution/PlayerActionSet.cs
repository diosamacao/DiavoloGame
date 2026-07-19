using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色出招表：绑定一张 ActionGraph（可含多个起手 Entry：攻击/闪避等）。
/// 物理输入映射不在本表配置；图中 ActionDefinition.Trigger 只保存语义意图。
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

    /// <summary>枚举图中全部有效玩法意图。</summary>
    public IEnumerable<GameplayIntentType> EnumerateTriggerIntents()
    {
        if (actionGraph == null)
            yield break;

        var set = new HashSet<GameplayIntentType>();
        actionGraph.CollectTriggerIntents(set);
        foreach (GameplayIntentType intent in set)
            yield return intent;
    }
}

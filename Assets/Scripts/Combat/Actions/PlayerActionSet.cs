using UnityEngine;

/// <summary>玩家出招表：普攻连段列表与闪避招式引用。</summary>
[CreateAssetMenu(fileName = "PlayerActionSet", menuName = "ACT/Combat/Player Action Set")]
public class PlayerActionSet : ScriptableObject
{
    [SerializeField] ActionDefinition[] attackChain = null!;
    [SerializeField] ActionDefinition dodge = null!;

    public ActionDefinition[] AttackChain => attackChain;
    public ActionDefinition Dodge => dodge;
}

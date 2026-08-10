using UnityEngine;

/// <summary>敌人 Brain 薄配置：仅木桩开关与生命周期；战斗距离/幅度真源在 BT 节点。</summary>
[CreateAssetMenu(fileName = "EnemyBrainProfile", menuName = "ACT/Enemy/Brain Profile")]
public sealed class EnemyBrainProfile : ScriptableObject
{
    [Header("Actions")]
    [Tooltip("关闭后不追击、不攻击（木桩）；仍响应受击/死亡门闩与 Reaction 表现。")]
    [SerializeField] bool enableCombatActions = true;

    [Header("Lifecycle")]
    [SerializeField] float deathDespawnDelaySeconds = 0.5f;

    /// <summary>为 false 时 Brain 不写移动/攻击输入（木桩）；Hit/Death 门闩仍生效。</summary>
    public bool EnableCombatActions => enableCombatActions;

    /// <summary>死亡表现完成后的额外回收等待。</summary>
    public float DeathDespawnDelaySeconds => Mathf.Max(0f, deathDespawnDelaySeconds);

    void OnValidate()
    {
        deathDespawnDelaySeconds = Mathf.Max(0f, deathDespawnDelaySeconds);
    }
}

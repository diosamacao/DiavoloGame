using UnityEngine;
using UnityEngine.Serialization;

/// <summary>敌人身份配置；组合角色身体、AI、生命值与受击/死亡表现。</summary>
[CreateAssetMenu(fileName = "EnemyDefinition", menuName = "ACT/Enemy/Enemy Definition")]
public sealed class EnemyDefinition : ScriptableObject
{
    [SerializeField] string displayName = "Enemy";
    [SerializeField] CharacterConfig characterConfig = null;
    [SerializeField] EnemyBrainProfile brainProfile = null;
    [SerializeField] float maxHp = 100f;
    [FormerlySerializedAs("teamIdOverride")]
    [SerializeField] int teamId = 1;

    /// <summary>用于运行时根节点与调试信息的显示名。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    /// <summary>角色模型、移动和动作图配置。</summary>
    public CharacterConfig CharacterConfig => characterConfig;
    /// <summary>敌人决策参数。</summary>
    public EnemyBrainProfile BrainProfile => brainProfile;
    /// <summary>最大生命值。</summary>
    public float MaxHp => Mathf.Max(1f, maxHp);
    /// <summary>敌人阵营；由 EnemyDefinition 独立持有，避免复用角色身体配置时继承玩家阵营。</summary>
    public int TeamId => teamId;

    /// <summary>校验敌人运行时必需引用。</summary>
    public bool Validate(UnityEngine.Object context)
    {
        bool valid = true;
        if (characterConfig == null)
        {
            Debug.LogError("EnemyDefinition: CharacterConfig 未配置。", context);
            valid = false;
        }
        else if (!characterConfig.ValidateForEnemy(context))
        {
            valid = false;
        }

        if (brainProfile == null)
        {
            Debug.LogError("EnemyDefinition: BrainProfile 未配置。", context);
            valid = false;
        }

        return valid;
    }
}

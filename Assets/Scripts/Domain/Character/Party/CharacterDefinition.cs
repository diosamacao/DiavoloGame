using UnityEngine;

/// <summary>可出战角色的稳定身份与战斗装配入口；养成数据只按 CharacterId 外挂。</summary>
[CreateAssetMenu(fileName = "CharacterDefinition", menuName = "ACT/Party/Character Definition")]
public sealed class CharacterDefinition : ScriptableObject
{
    [SerializeField] string characterId = string.Empty;
    [SerializeField] CharacterAssistStyle assistStyle = CharacterAssistStyle.MeleeParry;
    [SerializeField] CharacterConfig characterConfig = null;
    /// <summary>跨资产与存档使用的稳定角色标识。</summary>
    public CharacterId Id => new(characterId);

    /// <summary>极限支援的招架或回避类型。</summary>
    public CharacterAssistStyle AssistStyle => assistStyle;

    /// <summary>角色模型、移动、动作图与数值装配配置。</summary>
    public CharacterConfig CharacterConfig => characterConfig;

    /// <summary>校验稳定 Id 与现有角色战斗配置。</summary>
    public bool Validate(UnityEngine.Object context)
    {
        bool valid = true;
        if (!Id.IsValid)
        {
            Debug.LogError("CharacterDefinition: CharacterId 不能为空。", context != null ? context : this);
            valid = false;
        }

        if (characterConfig == null)
        {
            Debug.LogError("CharacterDefinition: 必须绑定 CharacterConfig。", context != null ? context : this);
            valid = false;
        }

        return valid;
    }
}

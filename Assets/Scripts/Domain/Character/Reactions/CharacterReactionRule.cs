using System;
using UnityEngine;

/// <summary>一条角色反应选择规则；按类型与可选 HitReactionId 映射到表现动作。</summary>
[Serializable]
public sealed class CharacterReactionRule
{
    [SerializeField] CharacterReactionType reactionType = CharacterReactionType.Hit;
    [Tooltip("命中反应 Id；Default Rule 勾选时忽略。")]
    [SerializeField] string reactionId = string.Empty;
    [Tooltip("该类型没有精确 Id 匹配时使用；同一类型只能配置一条默认规则。")]
    [SerializeField] bool defaultRule = false;
    [SerializeField] ActionDefinition action = null;

    /// <summary>规则处理的反应类型。</summary>
    public CharacterReactionType ReactionType => reactionType;

    /// <summary>规则匹配的命中反应 Id。</summary>
    public string ReactionId => reactionId ?? string.Empty;

    /// <summary>是否为该类型的默认规则。</summary>
    public bool IsDefault => defaultRule;

    /// <summary>规则解析出的表现动作。</summary>
    public ActionDefinition Action => action;
}

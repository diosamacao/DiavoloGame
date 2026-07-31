using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>角色受击与死亡表现映射；由上层控制器交给解析器选择具体动作。</summary>
[Serializable]
public sealed class CharacterReactionSet
{
    [Tooltip("未解析到受击动作时使用的默认固定逻辑帧数。")]
    [SerializeField] int defaultHitStunFrames = 21;
    [SerializeField] CharacterReactionRule[] rules = Array.Empty<CharacterReactionRule>();

    /// <summary>无受击动作时由 HitState 使用的默认硬直逻辑帧数。</summary>
    public int DefaultHitStunFrames => Mathf.Max(0, defaultHitStunFrames);

    /// <summary>按类型与反应 Id 查找动作；精确规则优先于该类型默认规则。</summary>
    public ActionDefinition Resolve(CharacterReactionType type, string reactionId)
    {
        CharacterReactionRule defaultRule = null;
        string normalizedId = reactionId ?? string.Empty;
        CharacterReactionRule[] availableRules = rules ?? Array.Empty<CharacterReactionRule>();
        for (int i = 0; i < availableRules.Length; i++)
        {
            CharacterReactionRule rule = availableRules[i];
            if (rule == null || rule.ReactionType != type || rule.Action == null)
                continue;

            if (rule.IsDefault)
            {
                defaultRule ??= rule;
                continue;
            }

            if (string.Equals(rule.ReactionId, normalizedId, StringComparison.Ordinal))
                return rule.Action;
        }

        return defaultRule?.Action;
    }

    /// <summary>校验同类型默认规则与精确 Id 唯一，避免运行时选择依赖数组顺序。</summary>
    public bool Validate(UnityEngine.Object context)
    {
        bool valid = true;
        var defaultTypes = new HashSet<CharacterReactionType>();
        var exactKeys = new HashSet<string>(StringComparer.Ordinal);
        CharacterReactionRule[] availableRules = rules ?? Array.Empty<CharacterReactionRule>();
        for (int i = 0; i < availableRules.Length; i++)
        {
            CharacterReactionRule rule = availableRules[i];
            if (rule == null || rule.Action == null)
                continue;

            if (rule.IsDefault)
            {
                if (!defaultTypes.Add(rule.ReactionType))
                {
                    Debug.LogError(
                        $"CharacterReactionSet: {rule.ReactionType} 存在多条默认规则。",
                        context);
                    valid = false;
                }

                continue;
            }

            if (string.IsNullOrEmpty(rule.ReactionId))
            {
                Debug.LogError(
                    $"CharacterReactionSet: {rule.ReactionType} 的非默认规则必须填写 ReactionId。",
                    context);
                valid = false;
                continue;
            }

            string key = $"{rule.ReactionType}|{rule.ReactionId}";
            if (!exactKeys.Add(key))
            {
                Debug.LogError($"CharacterReactionSet: 重复反应规则 {key}。", context);
                valid = false;
            }
        }

        return valid;
    }
}

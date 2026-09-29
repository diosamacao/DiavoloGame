using System.Collections.Generic;
using UnityEngine;

/// <summary>约定冷却 id，并统一成功 CD 与请求失败重试门控。</summary>
public static class EnemyCooldownIds
{
    /// <summary>基础攻击成功冷却；帧数由 CooldownGate 节点持有。</summary>
    public const string BasicAttack = "basic_attack";

    /// <summary>任意 Action Entry 请求失败后的全局短重试冷却。</summary>
    public const string ActionEntryRetry = "action_entry_retry";

    /// <summary>闪避类 Task / CooldownGate 默认 id。</summary>
    public const string Dodge = "dodge";

    /// <summary>判断节点冷却门是否就绪；Action Entry 失败重试期间所有招式门保持关闭。</summary>
    public static bool IsGateReady(EnemyCooldownTable cooldowns, string id)
    {
        if (cooldowns == null || !cooldowns.IsReady(id))
            return false;
        return cooldowns.IsReady(ActionEntryRetry);
    }
}

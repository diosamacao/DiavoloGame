using UnityEngine;

/// <summary>一次受击或死亡表现请求；由角色 Actor 写入并由对应顶层状态消费。</summary>
public readonly struct CharacterReactionRequest
{
    /// <summary>创建反应请求；Action 为空时仅按持续时间锁定角色。</summary>
    public CharacterReactionRequest(float durationSeconds, ActionDefinition action)
    {
        DurationSeconds = Mathf.Max(0f, durationSeconds);
        Action = action;
    }

    /// <summary>无反应动画时使用的状态持续时间。</summary>
    public float DurationSeconds { get; }

    /// <summary>可选受击或死亡动作表现。</summary>
    public ActionDefinition Action { get; }
}

using UnityEngine;

/// <summary>一次已由上层解析完成的角色反应播放请求。</summary>
public readonly struct CharacterReactionRequest
{
    /// <summary>创建播放请求；动作为空时仅按持续时间锁定角色。</summary>
    public CharacterReactionRequest(float durationSeconds, ActionDefinition resolvedAction)
    {
        DurationSeconds = Mathf.Max(0f, durationSeconds);
        ResolvedAction = resolvedAction;
    }

    /// <summary>无反应动画时使用的状态持续时间。</summary>
    public float DurationSeconds { get; }

    /// <summary>上层控制器已经选定的可选表现动作。</summary>
    public ActionDefinition ResolvedAction { get; }
}

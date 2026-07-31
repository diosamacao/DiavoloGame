/// <summary>一次已由上层解析完成的角色反应播放请求。</summary>
public readonly struct CharacterReactionRequest
{
    /// <summary>创建播放请求；动作为空时仅按整数逻辑帧数锁定角色。</summary>
    public CharacterReactionRequest(int durationFrames, ActionDefinition resolvedAction)
    {
        DurationFrames = durationFrames > 0 ? durationFrames : 0;
        ResolvedAction = resolvedAction;
    }

    /// <summary>无反应动作时使用的固定逻辑帧数。</summary>
    public int DurationFrames { get; }

    /// <summary>上层控制器已经选定的可选表现动作。</summary>
    public ActionDefinition ResolvedAction { get; }
}

using System.Collections.Generic;

/// <summary>保存当帧玩法意图与供 Action Cancel 跨帧消费的语义缓冲。</summary>
public sealed class GameplayIntentBuffer : IActionInputBuffer
{
    readonly List<GameplayIntentType> _frameIntents = new(4);
    readonly HashSet<GameplayIntentType> _bufferedIntents = new();

    /// <summary>本帧按生产顺序输出的去重意图。</summary>
    public IReadOnlyList<GameplayIntentType> FrameIntents => _frameIntents;

    /// <summary>开始新帧；只清当帧事件，不清跨帧 Cancel 缓冲。</summary>
    public void BeginFrame() => _frameIntents.Clear();

    /// <summary>输出一次语义意图；同帧同类型只保留一次。</summary>
    public void Emit(GameplayIntentType intent)
    {
        if (intent == GameplayIntentType.None || _frameIntents.Contains(intent))
            return;

        _frameIntents.Add(intent);
    }

    /// <summary>把意图放入动作 Cancel 缓冲。</summary>
    public void Buffer(GameplayIntentType intent)
    {
        if (intent != GameplayIntentType.None)
            _bufferedIntents.Add(intent);
    }

    /// <summary>查询指定语义是否仍在 Action Cancel 缓冲中。</summary>
    public bool HasBuffer(GameplayIntentType intent) =>
        intent != GameplayIntentType.None && _bufferedIntents.Contains(intent);

    /// <summary>消费指定语义缓冲；不存在时返回 false。</summary>
    public bool TryConsumeBuffer(GameplayIntentType intent) =>
        intent != GameplayIntentType.None && _bufferedIntents.Remove(intent);

    /// <summary>清除指定动作意图缓冲。</summary>
    public void ClearBuffer(GameplayIntentType intent) => _bufferedIntents.Remove(intent);

    /// <summary>清除全部动作意图缓冲。</summary>
    public void ClearAllBuffers() => _bufferedIntents.Clear();
}

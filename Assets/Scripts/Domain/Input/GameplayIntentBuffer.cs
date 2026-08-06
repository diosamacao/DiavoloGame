using System.Collections.Generic;

/// <summary>保存当帧玩法意图与供 Action Cancel 跨帧消费的语义缓冲。</summary>
public sealed class GameplayIntentBuffer : IActionInputBuffer
{
    readonly List<GameplayIntentType> _frameIntents = new(4);
    readonly Dictionary<GameplayIntentType, int> _bufferedIntents = new();
    readonly List<GameplayIntentType> _expiredIntents = new(4);
    readonly List<GameplayIntentType> _activeIntents = new(8);
    readonly int _bufferDurationFrames;

    /// <summary>创建带统一整数帧有效期的意图缓冲；至少保留一帧。</summary>
    public GameplayIntentBuffer(int bufferDurationFrames)
    {
        _bufferDurationFrames = System.Math.Max(1, bufferDurationFrames);
    }

    /// <summary>本帧按生产顺序输出的去重意图。</summary>
    public IReadOnlyList<GameplayIntentType> FrameIntents => _frameIntents;

    /// <summary>开始新帧；只清当帧事件，不清跨帧 Cancel 缓冲。</summary>
    public void BeginFrame() => _frameIntents.Clear();

    /// <summary>推进跨帧缓冲有效期并删除过期意图；在生产本帧意图前调用。</summary>
    public void Step()
    {
        if (_bufferedIntents.Count == 0)
            return;

        _expiredIntents.Clear();

        // Dictionary 迭代中不可写回，先收集过期项，再统一更新剩余时间。
        _activeIntents.Clear();
        foreach (GameplayIntentType intent in _bufferedIntents.Keys)
            _activeIntents.Add(intent);

        for (int i = 0; i < _activeIntents.Count; i++)
        {
            GameplayIntentType intent = _activeIntents[i];
            int remaining = _bufferedIntents[intent] - 1;
            if (remaining <= 0)
                _expiredIntents.Add(intent);
            else
                _bufferedIntents[intent] = remaining;
        }

        for (int i = 0; i < _expiredIntents.Count; i++)
            _bufferedIntents.Remove(_expiredIntents[i]);
    }

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
            _bufferedIntents[intent] = _bufferDurationFrames;
    }

    /// <summary>查询指定语义是否仍在 Action Cancel 缓冲中。</summary>
    public bool HasBuffer(GameplayIntentType intent) =>
        intent != GameplayIntentType.None && _bufferedIntents.ContainsKey(intent);

    /// <summary>消费指定语义缓冲；不存在时返回 false。</summary>
    public bool TryConsumeBuffer(GameplayIntentType intent) =>
        intent != GameplayIntentType.None && _bufferedIntents.Remove(intent);

    /// <summary>清除指定动作意图缓冲。</summary>
    public void ClearBuffer(GameplayIntentType intent) => _bufferedIntents.Remove(intent);

    /// <summary>清除全部动作意图缓冲。</summary>
    public void ClearAllBuffers() => _bufferedIntents.Clear();

    /// <summary>复制当前跨帧缓冲供 Debug HUD 只读展示；不修改缓冲状态。</summary>
    public int CopyBufferedForDebug(BufferedIntentDebug[] destination)
    {
        if (destination == null || destination.Length == 0)
            return 0;

        int written = 0;
        foreach (System.Collections.Generic.KeyValuePair<GameplayIntentType, int> pair in _bufferedIntents)
        {
            if (written >= destination.Length)
                break;
            destination[written++] = new BufferedIntentDebug(pair.Key, pair.Value);
        }

        return written;
    }
}

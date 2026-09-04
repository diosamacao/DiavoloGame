using System.Collections.Generic;

/// <summary>
/// 权威帧收集敌人 AssistCue 窗。Coordinator 只读 ActiveCue，不写 Timeline。
/// </summary>
public sealed class WorldAssistCueBoard
{
    readonly List<AssistCue> _cues = new(8);

    /// <summary>本帧已发布的闪光条数。</summary>
    public int Count => _cues.Count;

    /// <summary>逻辑步开始时清空，避免上一帧窗残留。</summary>
    public void BeginFrame() => _cues.Clear();

    /// <summary>登记一条当前帧仍生效的闪光；无效 Owner 忽略。</summary>
    public void Publish(in AssistCue cue)
    {
        if (!cue.IsValid)
            return;
        _cues.Add(cue);
    }

    /// <summary>
    /// 取本帧对外 Cue：优先 preferredOwner（锁定目标），否则 OwnerId 最小。
    /// 无窗返回 false。
    /// </summary>
    public bool TryGetActive(SimActorId preferredOwner, out AssistCue cue)
    {
        cue = default;
        if (_cues.Count == 0)
            return false;

        int best = -1;
        for (int i = 0; i < _cues.Count; i++)
        {
            AssistCue candidate = _cues[i];
            if (preferredOwner.IsValid && candidate.OwnerId.Equals(preferredOwner))
            {
                cue = candidate;
                return true;
            }

            if (best < 0 || candidate.OwnerId.Value < _cues[best].OwnerId.Value)
                best = i;
        }

        cue = _cues[best];
        return true;
    }
}

using System;
using System.Collections.Generic;

/// <summary>按逻辑帧与 Actor 保存输入历史，并为单机追帧展开连续状态。</summary>
public sealed class InputFrameBuffer
{
    readonly Dictionary<InputFrameKey, InputFrame> _frames = new();
    readonly Dictionary<SimActorId, InputFrame> _lastResolvedByActor = new();

    /// <summary>写入或覆盖一帧完整输入，供 AI、回放与未来权威包使用。</summary>
    public void Set(in InputFrame frame)
    {
        ValidateFrame(in frame);
        _frames[new InputFrameKey(frame.Frame, frame.ActorId)] = frame;
    }

    /// <summary>把同一目标帧的设备采样合并进槽位；边沿保留，连续状态取最后值。</summary>
    public void MergeLocalSample(in InputFrame sample)
    {
        ValidateFrame(in sample);
        var key = new InputFrameKey(sample.Frame, sample.ActorId);
        _frames[key] = _frames.TryGetValue(key, out InputFrame existing)
            ? existing.MergeSample(in sample)
            : sample;
    }

    /// <summary>读取精确记录；不存在时不进行连续状态展开。</summary>
    public bool TryGetExact(long frame, SimActorId actorId, out InputFrame input) =>
        _frames.TryGetValue(new InputFrameKey(frame, actorId), out input);

    /// <summary>
    /// 读取本帧输入；单机追帧缺少设备采样时仅延续上一帧 Move/Held，
    /// Pressed/Released 永不推导或重复。
    /// </summary>
    public InputFrame ResolveLocal(long frame, SimActorId actorId)
    {
        var key = new InputFrameKey(frame, actorId);
        if (_frames.TryGetValue(key, out InputFrame exact))
        {
            _lastResolvedByActor[actorId] = exact;
            return exact;
        }

        InputFrame resolved = _lastResolvedByActor.TryGetValue(actorId, out InputFrame previous)
            ? previous.CarryForward(frame)
            : InputFrame.Empty(frame, actorId);
        _frames[key] = resolved;
        _lastResolvedByActor[actorId] = resolved;
        return resolved;
    }

    /// <summary>移除指定帧之前的历史，保留边界帧供追帧与回放恢复。</summary>
    public void TrimBefore(long firstFrameToKeep)
    {
        if (_frames.Count == 0)
            return;

        var expired = new List<InputFrameKey>();
        foreach (InputFrameKey key in _frames.Keys)
        {
            if (key.Frame < firstFrameToKeep)
                expired.Add(key);
        }

        for (int i = 0; i < expired.Count; i++)
            _frames.Remove(expired[i]);
    }

    /// <summary>Actor 注销时清理其输入历史和连续状态。</summary>
    public void RemoveActor(SimActorId actorId)
    {
        var removed = new List<InputFrameKey>();
        foreach (InputFrameKey key in _frames.Keys)
        {
            if (key.ActorId == actorId)
                removed.Add(key);
        }

        for (int i = 0; i < removed.Count; i++)
            _frames.Remove(removed[i]);
        _lastResolvedByActor.Remove(actorId);
    }

    static void ValidateFrame(in InputFrame frame)
    {
        if (frame.Frame < 0)
            throw new ArgumentOutOfRangeException(nameof(frame), "输入逻辑帧不能为负数。");
        if (!frame.ActorId.IsValid)
            throw new ArgumentException("输入帧必须绑定有效 SimActorId。", nameof(frame));
    }

    /// <summary>输入历史字典的稳定复合键。</summary>
    readonly struct InputFrameKey : IEquatable<InputFrameKey>
    {
        readonly long _frame;
        readonly SimActorId _actorId;

        /// <summary>创建帧号与 ActorId 复合键。</summary>
        public InputFrameKey(long frame, SimActorId actorId)
        {
            _frame = frame;
            _actorId = actorId;
        }

        /// <summary>键中的逻辑帧。</summary>
        public long Frame => _frame;
        /// <summary>键中的稳定 ActorId。</summary>
        public SimActorId ActorId => _actorId;

        /// <summary>比较两个复合键。</summary>
        public bool Equals(InputFrameKey other) => _frame == other._frame && _actorId == other._actorId;
        /// <summary>比较装箱后的复合键。</summary>
        public override bool Equals(object obj) => obj is InputFrameKey other && Equals(other);

        /// <summary>按帧号与 ActorId 生成哈希。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (_frame.GetHashCode() * 397) ^ _actorId.GetHashCode();
            }
        }
    }
}

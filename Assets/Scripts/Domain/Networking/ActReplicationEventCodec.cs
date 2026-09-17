using System;
using System.Collections.Generic;

/// <summary>可靠命中事件包：本帧权威命中，不含 Snapshot 冗余窗口。</summary>
public static class ActReplicationEventCodec
{
    /// <summary>当前唯一支持的事件包版本；与单条命中线版本严格一致。</summary>
    public const byte Version = ActReplicatedHitEventCodec.Version;

    /// <summary>单包允许的最大命中数。</summary>
    public const int MaxHits = 1024;

    /// <summary>编码本帧命中数组。</summary>
    public static byte[] Encode(ReplicatedHitEvent[] hits)
    {
        hits ??= Array.Empty<ReplicatedHitEvent>();
        if (hits.Length > MaxHits)
            throw new NetBufferException($"命中数量 {hits.Length} 超过上限 {MaxHits}。");

        var writer = new NetBufferWriter();
        writer.WriteByte(Version);
        writer.WriteInt32(hits.Length);
        for (int i = 0; i < hits.Length; i++)
            ActReplicatedHitEventCodec.Write(writer, in hits[i]);
        return writer.ToArray();
    }

    /// <summary>严格解码完整事件包。</summary>
    public static ReplicatedHitEvent[] Decode(byte[] payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        var reader = new NetBufferReader(payload);
        byte version = reader.ReadByte();
        if (version != Version)
            throw new InvalidOperationException($"不支持复制事件版本 {version}。");

        int hitCount = reader.ReadLength(MaxHits);
        var hits = new ReplicatedHitEvent[hitCount];
        for (int i = 0; i < hitCount; i++)
            hits[i] = ActReplicatedHitEventCodec.Read(reader);
        reader.EnsureComplete();
        return hits;
    }

    /// <summary>解决 Owner 弹刀事件早于阵容 Meta 到达的身份竞态，并提供有界 pending/completed 去重。</summary>
    public sealed class OwnerAssistParryEventQueue
    {
        /// <summary>最多保留的待判定事件数；溢出项不标 completed，可靠重复仍可重新判定。</summary>
        public const int MaxPending = 64;

        const int MaxCompleted = 128;
        readonly Dictionary<SimHitKey, ReplicatedHitEvent> _pending = new();
        readonly List<SimHitKey> _pendingOrder = new();
        readonly HashSet<SimHitKey> _completed = new();
        readonly Queue<SimHitKey> _completedOrder = new();

        /// <summary>当前尚未能按稳定阵容身份判定的事件数。</summary>
        public int PendingCount => _pending.Count;

        /// <summary>尝试应用事件；只有成功应用或 Meta 已证明非本阵容时才完成去重。</summary>
        public void ApplyOrPend(
            in ReplicatedHitEvent hit,
            IReadOnlyList<SimActorId> authoritativePartyIds,
            bool authorityRosterReady,
            Func<ReplicatedHitEvent, bool> tryApply)
        {
            if (_completed.Contains(hit.Key))
                return;
            if (tryApply == null)
                throw new ArgumentNullException(nameof(tryApply));

            if (tryApply(hit))
            {
                Complete(hit.Key);
                RemovePending(hit.Key);
                return;
            }

            if (authorityRosterReady && !Contains(authoritativePartyIds, hit.Key.TargetId))
            {
                Complete(hit.Key);
                RemovePending(hit.Key);
                return;
            }

            if (_pending.ContainsKey(hit.Key))
                return;
            _pending.Add(hit.Key, hit);
            _pendingOrder.Add(hit.Key);
            TrimPending();
        }

        /// <summary>阵容 Meta 应用后重试全部 pending。</summary>
        public void Retry(
            IReadOnlyList<SimActorId> authoritativePartyIds,
            Func<ReplicatedHitEvent, bool> tryApply)
        {
            if (tryApply == null)
                throw new ArgumentNullException(nameof(tryApply));

            SimHitKey[] keys = new SimHitKey[_pending.Count];
            _pending.Keys.CopyTo(keys, 0);
            for (int i = 0; i < keys.Length; i++)
            {
                if (!_pending.TryGetValue(keys[i], out ReplicatedHitEvent hit))
                    continue;
                ApplyOrPend(in hit, authoritativePartyIds, authorityRosterReady: true, tryApply);
            }
        }

        /// <summary>清空跨 Session 的 pending 与 completed 身份。</summary>
        public void Clear()
        {
            _pending.Clear();
            _pendingOrder.Clear();
            _completed.Clear();
            _completedOrder.Clear();
        }

        /// <summary>判断权威阵容稳定 Id 是否包含事件目标。</summary>
        static bool Contains(IReadOnlyList<SimActorId> ids, SimActorId target)
        {
            if (ids == null)
                return false;
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] == target)
                    return true;
            return false;
        }

        /// <summary>把已处理键放入有界 completed 窗口。</summary>
        void Complete(SimHitKey key)
        {
            if (!_completed.Add(key))
                return;
            _completedOrder.Enqueue(key);
            while (_completedOrder.Count > MaxCompleted)
                _completed.Remove(_completedOrder.Dequeue());
        }

        /// <summary>淘汰最早 pending；淘汰不等于完成，重复可靠事件仍可重新判定。</summary>
        void TrimPending()
        {
            while (_pending.Count > MaxPending && _pendingOrder.Count > 0)
            {
                SimHitKey oldest = _pendingOrder[0];
                _pendingOrder.RemoveAt(0);
                _pending.Remove(oldest);
            }
        }

        /// <summary>同时移除 pending 正文与顺序索引。</summary>
        void RemovePending(SimHitKey key)
        {
            if (!_pending.Remove(key))
                return;
            _pendingOrder.Remove(key);
        }
    }

}

using System;
using System.Collections.Generic;

/// <summary>
/// 远端客户端出招预测：只记 ActionId/帧供 Clip 表现，不跑命中、不写 Numeric。
/// Listen Host 本地玩家不得使用本驱动。
/// </summary>
public sealed class PredictedActionDriver
{
    const int MaxPending = 180;

    readonly List<PendingAction> _pending = new(32);
    int _actionId;
    int _actionFrame;

    /// <summary>当前预测动作 Id；0 表示无招。</summary>
    public int ActionId => _actionId;

    /// <summary>当前预测动作帧。</summary>
    public int ActionFrame => _actionFrame;

    /// <summary>是否仍有预测招在播。</summary>
    public bool IsActive => _actionId != 0;

    /// <summary>尚未被权威确认的出招条数。</summary>
    public int PendingCount => _pending.Count;

    /// <summary>
    /// 权威未硬直时，用本机已解析的动作（同机预览=Host 当帧 Action）推进预测。
    /// 禁止在此调用命中收集。
    /// </summary>
    public void Predict(long frame, int actionId, int actionFrame)
    {
        _actionId = actionId < 0 ? 0 : actionId;
        _actionFrame = actionFrame < 0 ? 0 : actionFrame;
        RecordPending(frame);
    }

    /// <summary>
    /// 权威当帧已硬直/死亡：不跟当帧权威招，只推进已预测招的帧，等延迟 Tick 纠偏。
    /// </summary>
    public void TickUnconfirmed(long frame)
    {
        if (_actionId != 0)
            _actionFrame++;
        RecordPending(frame);
    }

    /// <summary>
    /// 用对应权威帧和解。只在该帧权威未起手、换招或 Hit/Death 时取消；
    /// 同一招只 Ack，禁止把延迟帧 Seek 回去。
    /// </summary>
    public PredictedActionReconcileResult Reconcile(
        long authorityFrame,
        in ActorReplicationSnapshot authority)
    {
        int predictedIdAtFrame = ResolvePredictedActionId(authorityFrame);
        DropAcked(authorityFrame);

        bool authorityStaggered = authority.VitalityEdge == VitalityReplicationEdge.Hit
            || authority.VitalityEdge == VitalityReplicationEdge.Death;

        if (authorityStaggered)
        {
            bool cancelled = predictedIdAtFrame != 0 && predictedIdAtFrame != authority.ActionId;
            ApplyAuthorityAction(in authority);
            return new PredictedActionReconcileResult(
                cancelled || authorityStaggered,
                _actionId,
                _actionFrame);
        }

        if (predictedIdAtFrame != 0 && authority.ActionId == 0)
        {
            _actionId = 0;
            _actionFrame = 0;
            return new PredictedActionReconcileResult(cancelled: true, 0, 0);
        }

        if (authority.ActionId != 0
            && predictedIdAtFrame != 0
            && authority.ActionId != predictedIdAtFrame)
        {
            ApplyAuthorityAction(in authority);
            return new PredictedActionReconcileResult(cancelled: true, _actionId, _actionFrame);
        }

        return new PredictedActionReconcileResult(cancelled: false, _actionId, _actionFrame);
    }

    /// <summary>把预测招改成权威当帧 Action（受击/换招）。</summary>
    void ApplyAuthorityAction(in ActorReplicationSnapshot authority)
    {
        _actionId = authority.ActionId;
        _actionFrame = authority.ActionFrame;
    }

    /// <summary>记下本帧预测招，供延迟 Tick 按 frame 对照。</summary>
    void RecordPending(long frame)
    {
        _pending.Add(new PendingAction(frame, _actionId, _actionFrame));
        if (_pending.Count > MaxPending)
            _pending.RemoveRange(0, _pending.Count - MaxPending);
    }

    /// <summary>取该权威帧当时预测的 ActionId；找不到则用当前值。</summary>
    int ResolvePredictedActionId(long authorityFrame)
    {
        for (int i = 0; i < _pending.Count; i++)
        {
            if (_pending[i].Frame == authorityFrame)
                return _pending[i].ActionId;
        }

        return _actionId;
    }

    /// <summary>丢弃该权威帧及更旧的出招缓存。</summary>
    void DropAcked(long authorityFrame)
    {
        int keepFrom = 0;
        while (keepFrom < _pending.Count && _pending[keepFrom].Frame <= authorityFrame)
            keepFrom++;

        if (keepFrom > 0)
            _pending.RemoveRange(0, keepFrom);
    }

    readonly struct PendingAction
    {
        public PendingAction(long frame, int actionId, int actionFrame)
        {
            Frame = frame;
            ActionId = actionId;
            ActionFrame = actionFrame;
        }

        public long Frame { get; }
        public int ActionId { get; }
        public int ActionFrame { get; }
    }
}

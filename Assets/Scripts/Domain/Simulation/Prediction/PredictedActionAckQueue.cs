using System.Collections.Generic;

/// <summary>
/// 出招预测 Ack：只记 (frame, actionId)，不解招、不播 Clip。
/// 权威未起手、变体分叉或 Hit/Death 时通知 Runner 停本地 ActionSim。
/// 连招超前（权威仍是本机已打过的上一招）只 Ack。
/// </summary>
public sealed class PredictedActionAckQueue
{
    const int MaxPending = 180;

    readonly List<PendingAction> _pending = new(32);

    /// <summary>尚未被权威确认的出招条数。</summary>
    public int PendingCount => _pending.Count;

    /// <summary>记下本帧预测招 Id，供延迟 Tick 按 frame 对照。</summary>
    public void Record(long frame, int actionId)
    {
        _pending.Add(new PendingAction(frame, actionId < 0 ? 0 : actionId));
        if (_pending.Count > MaxPending)
            _pending.RemoveRange(0, _pending.Count - MaxPending);
    }

    /// <summary>
    /// 用对应权威帧和解。同招只 Ack。
    /// 该帧预测起手但权威未起手、换招（且权威招不是本机已打过的上一招），或 Vitality Hit/Death：Cancelled。
    /// 本机已连到下一招、权威还停在上一招：只 Ack，不 Cancel。
    /// 禁止把延迟帧 Seek 回去。
    /// </summary>
    public PredictedActionReconcileResult Reconcile(
        long authorityFrame,
        in ActorReplicationSnapshot authority)
    {
        int predictedIdAtFrame = ResolvePredictedActionId(authorityFrame);
        // Drop 前先查：上一招记录在更早的 pending 里。
        bool authorityWasPriorLocal = HasRecordedActionBefore(authority.ActionId, authorityFrame);
        DropAcked(authorityFrame);

        bool authorityStaggered = authority.VitalityEdge == VitalityReplicationEdge.Hit
            || authority.VitalityEdge == VitalityReplicationEdge.Death;
        if (authorityStaggered)
        {
            return new PredictedActionReconcileResult(
                cancelled: true,
                authority.ActionId,
                authority.ActionFrame);
        }

        if (predictedIdAtFrame != 0 && authority.ActionId == 0)
            return new PredictedActionReconcileResult(cancelled: true, 0, 0);

        if (authority.ActionId != 0
            && predictedIdAtFrame != 0
            && authority.ActionId != predictedIdAtFrame)
        {
            // 连招超前：该帧预测已是下一招，权威仍是本机刚打过的上一招。
            if (authorityWasPriorLocal)
            {
                return new PredictedActionReconcileResult(
                    cancelled: false,
                    predictedIdAtFrame,
                    authority.ActionFrame);
            }

            return new PredictedActionReconcileResult(
                cancelled: true,
                authority.ActionId,
                authority.ActionFrame);
        }

        return new PredictedActionReconcileResult(
            cancelled: false,
            predictedIdAtFrame,
            authority.ActionFrame);
    }

    /// <summary>
    /// 本机预测招仍在播则跟预测；自然结束后禁止用延迟权威招重播 Clip/VFX。
    /// 仅受击或和解真取消后才跟权威招。
    /// </summary>
    public static bool ShouldPresentAuthorityAction(
        bool localActionActive,
        bool suppressStaleAuthorityAction,
        bool authorityHitOrDeath,
        int authorityActionId)
    {
        if (localActionActive)
            return false;
        if (authorityHitOrDeath)
            return true;
        if (authorityActionId == 0)
            return false;
        return !suppressStaleAuthorityAction;
    }

    /// <summary>该帧之前是否已记录过同一招（连招上一招仍停在权威侧）。</summary>
    bool HasRecordedActionBefore(int actionId, long frame)
    {
        if (actionId == 0)
            return false;

        for (int i = 0; i < _pending.Count; i++)
        {
            if (_pending[i].Frame < frame && _pending[i].ActionId == actionId)
                return true;
        }

        return false;
    }

    /// <summary>取该权威帧当时预测的 ActionId；找不到则为 0。</summary>
    int ResolvePredictedActionId(long authorityFrame)
    {
        for (int i = 0; i < _pending.Count; i++)
        {
            if (_pending[i].Frame == authorityFrame)
                return _pending[i].ActionId;
        }

        return 0;
    }

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
        public PendingAction(long frame, int actionId)
        {
            Frame = frame;
            ActionId = actionId;
        }

        public long Frame { get; }
        public int ActionId { get; }
    }
}

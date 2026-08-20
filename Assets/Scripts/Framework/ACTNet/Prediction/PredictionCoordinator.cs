using System;
using System.Collections.Generic;

/// <summary>
/// 通用预测协调：记账、Ack、对照、按策略 Restore + Replay。
/// 禁止在此判断 ActionId、Hit、Death 或 2m 阈值。
/// </summary>
public sealed class PredictionCoordinator<TCommand, TState>
{
    readonly IPredictionModel<TCommand, TState> _model;
    readonly CommandHistory<TCommand> _commands;
    readonly PredictedStateHistory<TState> _states;
    readonly PredictionAckTracker _ack = new();

    /// <summary>绑定业务模型与历史容量。</summary>
    public PredictionCoordinator(IPredictionModel<TCommand, TState> model, int maxPending = 180)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _commands = new CommandHistory<TCommand>(maxPending);
        _states = new PredictedStateHistory<TState>(maxPending);
        Metrics = new ReconcileMetrics();
    }

    /// <summary>尚未确认的命令数。</summary>
    public int PendingCount => _commands.Count;

    /// <summary>最近一次权威 Ack Tick；尚未 Ack 为 -1。</summary>
    public long LastAckTick => _ack.LastAckTick;

    /// <summary>纠偏计数，供 HUD 显示。</summary>
    public ReconcileMetrics Metrics { get; }

    /// <summary>记录本 Tick 命令与预测状态。</summary>
    public void Record(long tick, in TCommand command, in TState predictedState)
    {
        _commands.Record(tick, in command);
        _states.Capture(tick, in predictedState);
        Metrics.RecordAcknowledge(Metrics.LastError, _commands.Count);
    }

    /// <summary>读取 Ack 帧预测误差，不改历史；供业务层算策略。</summary>
    public int PeekError(long ackTick, in TState authority) =>
        MeasureAgainstAck(ackTick, in authority);

    /// <summary>
    /// 对照权威状态。策略由业务模型事先算好；本方法只执行 Ack / Restore / Replay。
    /// </summary>
    public PredictionReconcileResult ReceiveAuthority(
        long ackTick,
        in TState authority,
        in PredictionCorrectionPolicy policy)
    {
        int error = MeasureAgainstAck(ackTick, in authority);
        _ack.Acknowledge(ackTick);
        _commands.DropAcknowledged(ackTick);
        _states.DropAcknowledged(ackTick);

        if (!policy.CorrectionRequired)
        {
            Metrics.RecordAcknowledge(error, _commands.Count);
            return new PredictionReconcileResult(false, error, 0);
        }

        _model.Restore(in authority);
        int replayed = policy.AllowReplay ? ReplayUnacknowledged(in policy) : 0;
        Metrics.RecordCorrection(error, replayed, _commands.Count);
        return new PredictionReconcileResult(true, error, replayed);
    }

    int MeasureAgainstAck(long ackTick, in TState authority)
    {
        if (_states.TryGet(ackTick, out TState predicted))
            return _model.MeasureError(in authority, in predicted);
        TState current = _model.Capture();
        return _model.MeasureError(in authority, in current);
    }

    int ReplayUnacknowledged(in PredictionCorrectionPolicy policy)
    {
        int replayed = 0;
        var captured = new List<(long tick, TCommand command)>(_commands.Count);
        _commands.ForEachUnacknowledged((tick, command) => captured.Add((tick, command)));
        for (int i = 0; i < captured.Count; i++)
        {
            TCommand command = captured[i].command;
            if (!_model.TrySimulate(in command, in policy))
                continue;
            replayed++;
            _states.Capture(captured[i].tick, _model.Capture());
        }

        return replayed;
    }
}

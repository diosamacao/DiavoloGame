using NUnit.Framework;

/// <summary>GF5：假线性实体证明 Coordinator 可预测、注入分歧、Restore + Replay。</summary>
public sealed class FakeLinearEntityPredictionTests
{
    /// <summary>无分歧时对照只 Ack，位置保持预测结果。</summary>
    [Test]
    public void ReceiveAuthority_MatchingState_DoesNotRestore()
    {
        var model = new LinearModel();
        var coordinator = new PredictionCoordinator<LinearCommand, LinearState>(model);
        PredictSteps(coordinator, model, startTick: 1, count: 5, delta: 3);

        PredictionReconcileResult result = coordinator.ReceiveAuthority(
            3,
            new LinearState(9),
            PredictionCorrectionPolicy.AcknowledgeOnly);

        Assert.That(result.Snapped, Is.False);
        Assert.That(result.Error, Is.Zero);
        Assert.That(result.ReplayedCommands, Is.Zero);
        Assert.That(model.X, Is.EqualTo(15));
        Assert.That(coordinator.PendingCount, Is.EqualTo(2));
    }

    /// <summary>注入分歧后 Restore 到权威，再 Replay 未确认命令。</summary>
    [Test]
    public void ReceiveAuthority_InjectedDivergence_RestoresAndReplays()
    {
        var model = new LinearModel();
        var coordinator = new PredictionCoordinator<LinearCommand, LinearState>(model);
        PredictSteps(coordinator, model, startTick: 1, count: 5, delta: 3);

        var policy = new PredictionCorrectionPolicy(
            correctionRequired: true,
            allowReplay: true,
            replayKind: 0);
        PredictionReconcileResult result = coordinator.ReceiveAuthority(
            2,
            new LinearState(0),
            policy);

        Assert.That(result.Snapped, Is.True);
        Assert.That(result.Error, Is.EqualTo(6));
        Assert.That(result.ReplayedCommands, Is.EqualTo(3));
        Assert.That(model.X, Is.EqualTo(9));
        Assert.That(coordinator.PendingCount, Is.EqualTo(3));
        Assert.That(coordinator.Metrics.SnapCount, Is.EqualTo(1));
        Assert.That(coordinator.Metrics.ReplayCount, Is.EqualTo(3));
    }

    /// <summary>策略禁止 Replay 时只 Restore。</summary>
    [Test]
    public void ReceiveAuthority_CorrectionWithoutReplay_OnlyRestores()
    {
        var model = new LinearModel();
        var coordinator = new PredictionCoordinator<LinearCommand, LinearState>(model);
        PredictSteps(coordinator, model, startTick: 1, count: 3, delta: 4);

        var policy = new PredictionCorrectionPolicy(
            correctionRequired: true,
            allowReplay: false,
            replayKind: 0);
        PredictionReconcileResult result = coordinator.ReceiveAuthority(
            1,
            new LinearState(0),
            policy);

        Assert.That(result.Snapped, Is.True);
        Assert.That(result.ReplayedCommands, Is.Zero);
        Assert.That(model.X, Is.Zero);
    }

    static void PredictSteps(
        PredictionCoordinator<LinearCommand, LinearState> coordinator,
        LinearModel model,
        int startTick,
        int count,
        int delta)
    {
        for (int i = 0; i < count; i++)
        {
            var command = new LinearCommand(delta);
            model.X += delta;
            coordinator.Record(startTick + i, command, model.Capture());
        }
    }

    readonly struct LinearCommand
    {
        public LinearCommand(int deltaX) => DeltaX = deltaX;

        public int DeltaX { get; }
    }

    readonly struct LinearState
    {
        public LinearState(int x) => X = x;

        public int X { get; }
    }

    /// <summary>X += Delta；误差为绝对差。不含 ACT 类型。</summary>
    sealed class LinearModel : IPredictionModel<LinearCommand, LinearState>
    {
        public int X { get; set; }

        public LinearState Capture() => new LinearState(X);

        public void Restore(in LinearState authorityState) => X = authorityState.X;

        public bool TrySimulate(in LinearCommand command, in PredictionCorrectionPolicy policy)
        {
            X += command.DeltaX;
            return true;
        }

        public int MeasureError(in LinearState authority, in LinearState predicted)
        {
            int error = predicted.X - authority.X;
            return error < 0 ? -error : error;
        }
    }
}

using System;

/// <summary>Dedicated 固定帧协调器：独占命令合并、外部时钟推进与帧末 Guest 生命周期顺序。</summary>
public sealed class AuthorityStepCoordinator : IDisposable
{
    readonly SimulationHost _host;
    readonly AuthorityGuestRegistry _guests;
    readonly ActAuthorityReplicationAdapter _authority;
    readonly AuthorityReplicationPublisher _publisher;
    readonly ServerSimulationRunner _runner;
    bool _disposed;

    /// <summary>绑定 Host、Guest 注册表、命令适配器与帧末复制发布器。</summary>
    public AuthorityStepCoordinator(
        SimulationHost host,
        AuthorityGuestRegistry guests,
        ActAuthorityReplicationAdapter authority,
        AuthorityReplicationPublisher publisher)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _guests = guests ?? throw new ArgumentNullException(nameof(guests));
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _host.DriveFromExternalClock = true;
        _host.AfterLogicStep += OnAfterLogicStep;
        _runner = new ServerSimulationRunner(new SimulationStepKernel(), _host.StepOnce);
    }

    /// <summary>最近完成的权威逻辑帧。</summary>
    public long CurrentFrame => _host.CurrentFrame;

    /// <summary>最近一次固定帧 Runner 指标。</summary>
    public SimulationTickMetrics TickMetrics => _runner.Metrics;

    /// <summary>权威追帧核的渲染插值比例。</summary>
    public float InterpolationAlpha => _runner.InterpolationAlpha;

    /// <summary>合并该连接未应用命令，并在切人边沿到达时先更新 Active Guest。</summary>
    public void ApplyCommands(NetConnectionId connectionId, ClientCommand[] commands)
    {
        if (!_guests.TryGet(connectionId, out ActGameGuest guest)
            || guest.Actor == null
            || _host.World == null)
        {
            return;
        }

        long targetFrame = _host.CurrentFrame + 1;
        if (RoomRemoteInputMerge.TryMergeUnapplied(
                commands,
                guest.LastAppliedFrameHint,
                targetFrame,
                guest.Actor.SimulationId,
                out InputFrame preview,
                out _,
                out _)
            && guest.CanAcceptGameplayInput
            && preview.WasPressed(InputButton.SwitchCharacter))
        {
            guest.TryResolveSwitch();
        }

        ActAuthorityInputApplyResult result = _authority.ApplyGuestCommands(
            _host.World.InputFrames,
            _host.CurrentFrame,
            guest.Actor.SimulationId,
            commands,
            guest.LastAppliedFrameHint,
            guest.CanAcceptGameplayInput);
        if (!result.Applied)
            return;

        guest.LastAppliedFrameHint = result.NewestHint;
        guest.AppliedHintThisTick = result.FirstAppliedHint;
    }

    /// <summary>采样渲染输入并按单调时间推进权威固定帧。</summary>
    public void Advance(long nowMs)
    {
        _host.SampleRenderInputs();
        _runner.Advance(nowMs);
        _host.PublishExternalInterpolationAlpha(_runner.InterpolationAlpha);
    }

    /// <summary>只读预览下一次 Advance 会推进的逻辑步数。</summary>
    public int PeekAdvanceSteps(long nowMs) => _runner.PeekAdvanceSteps(nowMs);

    /// <summary>解除 Host 帧末订阅；重复释放为空操作。</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _host.AfterLogicStep -= OnAfterLogicStep;
    }

    /// <summary>固定保持 Guest 生命周期先提交、随后 Capture/Publish 的帧末顺序。</summary>
    void OnAfterLogicStep(long authorityFrame)
    {
        if (_guests.Count == 0)
            return;
        _guests.AdvancePostLogicLifecycles();
        _publisher.PublishFrame(authorityFrame);
    }
}

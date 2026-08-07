using System;
using System.Collections.Generic;

/// <summary>以整数逻辑帧执行起手、取消、命中衔接与自然结束的纯 C# 动作模拟核。</summary>
public sealed class ActionSim : IActionSimHitReceiver
{
    /// <summary>ActionSim 唯一接受的权威动作采样率。</summary>
    public const int LogicHz = 60;

    readonly IActionSimResolver _resolver;
    readonly IActionInputBuffer _inputBuffer;
    readonly IActionResourceGate _resourceGate;
    readonly Action<GameplayIntentType> _onBegun;
    readonly HashSet<GameplayIntentType> _candidateIntents = new HashSet<GameplayIntentType>();
    readonly HashSet<GameplayIntentType> _routeCandidateIntents = new HashSet<GameplayIntentType>();
    readonly List<GameplayIntentType> _bufferedIntents = new List<GameplayIntentType>(8);
    readonly List<ActionSimEvent> _events = new List<ActionSimEvent>(16);

    IActionSimContent _content;
    IActionSimGraph _graph;
    string _nodeId;
    int _currentFrame;
    int _instanceId;
    int _nextInstanceId;
    int _lastEndedInstanceId;
    bool _hasConfirmedHit;
    int _freezeFrames;
    bool _hitStopAppliedForInstance;
    ActionSimResolveResult _pendingTransition;
    bool _hasPendingTransition;
    bool _pendingStop;

    /// <summary>
    /// 创建动作模拟核；Resolver / 输入缓冲 / 资源 Gate / 起手回调可为空。
    /// onBegun：Begin 成功后按 Intent 通知（如清完美反击缓冲），Simulation 不引用 Numeric。
    /// </summary>
    public ActionSim(
        IActionSimResolver resolver = null,
        IActionInputBuffer inputBuffer = null,
        IActionResourceGate resourceGate = null,
        Action<GameplayIntentType> onBegun = null)
    {
        _resolver = resolver;
        _inputBuffer = inputBuffer;
        _resourceGate = resourceGate;
        _onBegun = onBegun;
    }

    /// <summary>当前权威动作帧；无活动动作时返回 0。</summary>
    public int CurrentFrame => IsActive ? _currentFrame : 0;

    /// <summary>当前动作实例的单调稳定 Id；无活动动作时返回 0。</summary>
    public int InstanceId => _instanceId;

    /// <summary>当前是否持有活动动作。</summary>
    public bool IsActive => _content != null;

    /// <summary>当前动作是否已到达 TotalFrames 终止哨兵。</summary>
    public bool IsComplete => IsActive && _currentFrame >= _content.TotalFrames;

    /// <summary>当前动作实例是否已确认至少一次命中。</summary>
    public bool HasConfirmedHit => _hasConfirmedHit;

    /// <summary>剩余逻辑卡肉帧；大于 0 时本 Step 不推进动作帧。</summary>
    public int FreezeFrames => _freezeFrames > 0 ? _freezeFrames : 0;

    /// <summary>是否处于逻辑卡肉。</summary>
    public bool IsFrozen => FreezeFrames > 0;

    /// <summary>返回指定动作实例是否已结束或被另一实例替换。</summary>
    public bool HasEndedActionInstance(int instanceId) =>
        instanceId > 0 && _lastEndedInstanceId == instanceId;

    /// <summary>当前 Recovery 帧是否允许移动取消。</summary>
    public bool CanCancelByMovement =>
        IsActive
        && !IsComplete
        && !IsFrozen
        && _content.AllowsMovementCancelAtFrame(_currentFrame);

    /// <summary>获取当前状态的只读值快照。</summary>
    public ActionSimSnapshot Snapshot =>
        new ActionSimSnapshot(
            _content,
            _graph,
            _nodeId,
            CurrentFrame,
            InstanceId,
            _hasConfirmedHit,
            IsActive,
            FreezeFrames);

    /// <summary>仅在当前无动作且内容已迁移为 60Hz 模拟数据时立即从 frame 0 起手。</summary>
    public bool TryStart(in ActionSimResolveResult result)
    {
        if (IsActive || !CanBegin(result) || !CanAfford(result.Content))
            return false;

        Begin(in result);
        return true;
    }

    /// <summary>供 Graph 同键选招与 Driver 预判；只读不扣费。</summary>
    public bool CanAffordContent(IActionSimContent content) => CanAfford(content);

    /// <summary>推进一个 World 帧；先提交上帧决定，再按每步一帧推进并解析取消或 Recovery。</summary>
    public void Step()
    {
        // 卡肉优先：冻结期间不提交切招、不推进动作帧、不解析 Cancel。
        if (_freezeFrames > 0)
        {
            _freezeFrames--;
            return;
        }

        bool committedTransition = CommitPendingDecision();
        if (!IsActive)
            return;

        // 刚提交的目标动作已在本 World 帧派发 frame 0，不得再次推进到 frame 1。
        if (!committedTransition)
        {
            int previousFrame = _currentFrame;
            _currentFrame = Math.Min(_currentFrame + 1, _content.TotalFrames);
            DispatchCrossedFrame(previousFrame, _currentFrame);
        }

        // TotalFrames 是纯退出哨兵；本帧禁止再消费 Cancel 或 Recovery 输入。
        if (IsComplete)
            return;

        if (TryQueueCancel())
            return;

        TryQueueRecoveryStart();
    }

    /// <summary>战斗统一结算后解析 OnHit/OnWhiff 自动衔接，并排队自然停止。</summary>
    public void ResolvePostCombat()
    {
        if (!IsActive || _hasPendingTransition || _pendingStop)
            return;

        // 卡肉期间仍可排队 OnHit/OnWhiff；自然结束推迟到解冻后。
        if (_graph != null
            && !string.IsNullOrEmpty(_nodeId)
            && _graph.TryResolveAutomaticTransition(
                _nodeId,
                _content,
                _currentFrame,
                _hasConfirmedHit,
                out ActionSimResolveResult result,
                out bool shouldStop))
        {
            if (shouldStop)
                _pendingStop = true;
            else
                QueueTransition(in result);
            return;
        }

        if (IsFrozen)
            return;

        if (IsComplete)
            Stop();
    }

    /// <summary>当前帧可中断且候选优先级严格更高时，立即硬切并覆盖待提交决定。</summary>
    public bool TryInterrupt(in ActionSimResolveResult result)
    {
        if (!IsActive
            || !CanBegin(result)
            || !CanAfford(result.Content)
            || result.Content.InterruptPriority <= _content.InterruptPriority
            || !_content.IsInterruptibleAtFrame(_currentFrame))
        {
            return false;
        }

        ClearOtherActionBuffers(result.Intent);
        ClearPendingDecision();
        EndCurrent();
        Begin(in result);
        return true;
    }

    /// <summary>仅接受与当前动作实例 Id 完全匹配的命中确认。</summary>
    public bool ConfirmHit(int actionInstanceId)
    {
        if (!IsActive || actionInstanceId <= 0 || actionInstanceId != _instanceId)
            return false;

        if (!_hasConfirmedHit)
        {
            _hasConfirmedHit = true;
            _events.Add(new ActionSimEvent(
                ActionSimEventType.HitConfirmed,
                _content,
                _graph,
                _nodeId,
                _currentFrame,
                _currentFrame,
                _instanceId));
        }

        return true;
    }

    /// <summary>帧末结算写入逻辑卡肉；同实例可延长剩余帧，oncePerAction 时拒绝第二次。</summary>
    public bool RequestHitStop(int actionInstanceId, int frames, bool oncePerAction)
    {
        if (!IsActive || actionInstanceId <= 0 || actionInstanceId != _instanceId || frames <= 0)
            return false;

        if (oncePerAction && _hitStopAppliedForInstance)
            return false;

        _freezeFrames = Math.Max(_freezeFrames, frames);
        if (oncePerAction)
            _hitStopAppliedForInstance = true;
        return true;
    }

    /// <summary>立即停止当前动作并清除尚未提交的停止或切招决定。</summary>
    public void Stop()
    {
        ClearPendingDecision();
        EndCurrent();
    }

    /// <summary>按产生顺序复制并清空全部待消费动作事件。</summary>
    public int DrainEvents(List<ActionSimEvent> destination)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        int count = _events.Count;
        destination.AddRange(_events);
        _events.Clear();
        return count;
    }

    /// <summary>验证解析内容已迁移完成、帧范围有效且采样率严格为 60Hz。</summary>
    static bool CanBegin(in ActionSimResolveResult result) =>
        result.IsValid
        && result.Content.IsSimulationReady
        && result.Content.SampleRate == LogicHz
        && result.Content.TotalFrames > 0;

    /// <summary>资源 Gate 鉴权；无 Gate 时视为可负担。</summary>
    bool CanAfford(IActionSimContent content) =>
        _resourceGate == null || content == null || _resourceGate.CanAfford(content);

    /// <summary>建立新的稳定动作实例，扣费一次，并立即派发 Started 与 frame 0 内容事件。</summary>
    void Begin(in ActionSimResolveResult result)
    {
        _content = result.Content;
        _graph = result.HasGraphCursor ? result.Graph : null;
        _nodeId = result.HasGraphCursor ? result.NodeId : null;
        _currentFrame = 0;
        _instanceId = checked(++_nextInstanceId);
        _hasConfirmedHit = false;
        _freezeFrames = 0;
        _hitStopAppliedForInstance = false;

        // 价签扣费与 Begin 同事务：仅成功起手路径调用一次
        _resourceGate?.CommitCost(_content);
        // 起手副作用（如 ClearPerfectDodgeCounter）经回调注入，覆盖 Start/Interrupt/Cancel
        _onBegun?.Invoke(result.Intent);

        _events.Add(new ActionSimEvent(
            ActionSimEventType.Started,
            _content,
            _graph,
            _nodeId,
            0,
            -1,
            _instanceId));
        _events.Add(new ActionSimEvent(
            ActionSimEventType.FrameAdvanced,
            _content,
            _graph,
            _nodeId,
            0,
            -1,
            _instanceId));
    }

    /// <summary>结束当前实例并保留单调 Id 计数器供后续动作使用。</summary>
    void EndCurrent()
    {
        if (!IsActive)
            return;

        _events.Add(new ActionSimEvent(
            ActionSimEventType.Stopped,
            _content,
            _graph,
            _nodeId,
            _currentFrame,
            _currentFrame,
            _instanceId));
        _lastEndedInstanceId = _instanceId;
        _content = null;
        _graph = null;
        _nodeId = null;
        _currentFrame = 0;
        _instanceId = 0;
        _hasConfirmedHit = false;
        _freezeFrames = 0;
        _hitStopAppliedForInstance = false;
    }

    /// <summary>派发跨过的全部帧；终止哨兵也交给外层时间轴生成区间 Exit。</summary>
    void DispatchCrossedFrame(int previousFrame, int targetFrame)
    {
        for (int frame = previousFrame + 1; frame <= targetFrame; frame++)
        {
            _events.Add(new ActionSimEvent(
                ActionSimEventType.FrameAdvanced,
                _content,
                _graph,
                _nodeId,
                frame,
                frame - 1,
                _instanceId));
        }
    }

    /// <summary>按窗口和输入优先级解析取消，成功时只排队到下一 World 帧。</summary>
    bool TryQueueCancel()
    {
        if (_resolver == null || _inputBuffer == null)
            return false;

        bool perfectActive =
            _content.IsCancelWindowActiveAtFrame(CancelWindowType.Perfect, _currentFrame);
        bool normalActive =
            _content.IsCancelWindowActiveAtFrame(CancelWindowType.Normal, _currentFrame);
        if (!perfectActive && !normalActive)
            return false;

        _candidateIntents.Clear();
        if (_graph != null && !string.IsNullOrEmpty(_nodeId))
        {
            if (perfectActive)
                CollectRouteCandidates(CancelWindowType.Perfect);
            if (normalActive)
                CollectRouteCandidates(CancelWindowType.Normal);
        }
        else
        {
            foreach (GameplayIntentType intent in _resolver.EnumerateActiveIntents())
                _candidateIntents.Add(intent);
        }

        CollectBufferedIntentsSorted();
        for (int i = 0; i < _bufferedIntents.Count; i++)
        {
            GameplayIntentType intent = _bufferedIntents[i];
            if (perfectActive && TryQueueCancelIntent(intent, CancelWindowType.Perfect))
                return true;
            if (normalActive && TryQueueCancelIntent(intent, CancelWindowType.Normal))
                return true;
        }

        return false;
    }

    /// <summary>把指定图路由候选合并到当前帧候选集合。</summary>
    void CollectRouteCandidates(CancelWindowType windowType)
    {
        _routeCandidateIntents.Clear();
        _graph.CollectCancelCandidateIntents(_nodeId, windowType, _routeCandidateIntents);
        _candidateIntents.UnionWith(_routeCandidateIntents);
    }

    /// <summary>解析并消费单个取消意图；切招结果延迟提交。不够费时不消费缓冲。</summary>
    bool TryQueueCancelIntent(GameplayIntentType intent, CancelWindowType windowType)
    {
        ActionSimSnapshot snapshot = Snapshot;
        if (!_resolver.TryResolveNext(intent, windowType, in snapshot, out ActionSimResolveResult result)
            || !CanBegin(result)
            || !CanAfford(result.Content))
        {
            return false;
        }

        _inputBuffer.TryConsumeBuffer(intent);
        ClearOtherActionBuffers(intent);
        QueueTransition(in result);
        return true;
    }

    /// <summary>在 Recovery 允许重开时按缓冲优先级解析图入口，并延迟提交。</summary>
    bool TryQueueRecoveryStart()
    {
        if (_resolver == null
            || _inputBuffer == null
            || !_content.AllowsRecoveryEntryRestartAtFrame(_currentFrame))
        {
            return false;
        }

        _candidateIntents.Clear();
        foreach (GameplayIntentType intent in _resolver.EnumerateActiveIntents())
            _candidateIntents.Add(intent);

        CollectBufferedIntentsSorted();
        for (int i = 0; i < _bufferedIntents.Count; i++)
        {
            GameplayIntentType intent = _bufferedIntents[i];
            ActionSimSnapshot snapshot = Snapshot;
            if (!_resolver.TryResolveRecoveryStart(
                    intent,
                    in snapshot,
                    out ActionSimResolveResult result)
                || !CanBegin(result)
                || !CanAfford(result.Content))
            {
                continue;
            }

            _inputBuffer.TryConsumeBuffer(intent);
            ClearOtherActionBuffers(intent);
            QueueTransition(in result);
            return true;
        }

        return false;
    }

    /// <summary>筛出实际已缓冲意图，并按取消优先级和枚举值确定性排序。</summary>
    void CollectBufferedIntentsSorted()
    {
        _bufferedIntents.Clear();
        foreach (GameplayIntentType intent in _candidateIntents)
        {
            if (_inputBuffer.HasBuffer(intent))
                _bufferedIntents.Add(intent);
        }

        _bufferedIntents.Sort(CompareCancelIntentPriority);
    }

    /// <summary>高取消优先级排前；同级按枚举值稳定排序。</summary>
    static int CompareCancelIntentPriority(GameplayIntentType left, GameplayIntentType right)
    {
        int priorityOrder = GameplayIntentCancelPriority.Get(right)
            .CompareTo(GameplayIntentCancelPriority.Get(left));
        return priorityOrder != 0 ? priorityOrder : left.CompareTo(right);
    }

    /// <summary>清除除策略明确保留项之外的其它动作意图缓冲。</summary>
    void ClearOtherActionBuffers(GameplayIntentType consumedIntent)
    {
        if (_resolver == null || _inputBuffer == null)
            return;

        foreach (GameplayIntentType intent in _resolver.EnumerateActiveIntents())
        {
            if (!GameplayIntentCancelPriority.ShouldRetainAfterConsume(consumedIntent, intent))
                _inputBuffer.TryConsumeBuffer(intent);
        }
    }

    /// <summary>记录本帧解析出的切招，供下一 World 帧开始时提交。</summary>
    void QueueTransition(in ActionSimResolveResult result)
    {
        if (_hasPendingTransition || _pendingStop || !CanBegin(result) || !CanAfford(result.Content))
            return;

        _pendingTransition = result;
        _hasPendingTransition = true;
    }

    /// <summary>提交上一 World 帧的停止或切招；返回是否刚提交了目标 frame 0。</summary>
    bool CommitPendingDecision()
    {
        if (_pendingStop)
        {
            ClearPendingDecision();
            EndCurrent();
            return false;
        }

        if (!_hasPendingTransition)
            return false;

        ActionSimResolveResult transition = _pendingTransition;
        ClearPendingDecision();
        // 二次 CanAfford：Cancel 排队后费用可能已变；不够则丢弃切招，缓冲由上层保留策略处理
        if (!CanAfford(transition.Content))
            return false;

        EndCurrent();
        Begin(in transition);
        return true;
    }

    /// <summary>清空尚未提交的帧边界决定，供停止或硬打断覆盖。</summary>
    void ClearPendingDecision()
    {
        _pendingTransition = default(ActionSimResolveResult);
        _hasPendingTransition = false;
        _pendingStop = false;
    }
}

using UnityEngine;

/// <summary>
/// 敌人 AI 宿主：门闩 + 黑板填装 + IEnemyBehaviorRunner Tick；
/// 帧末只提交通用 LocomotionDesire + ActionEntryRequest（无假手柄 / InputFrame 战斗提交）。
/// 战斗半径/幅度/仇恨滞回由 BT 节点负责；本类不读 Profile 战斗表。
/// </summary>
public sealed class EnemyBrain
{
    /// <summary>起手失败后的防抖逻辑帧（薄 Brain 辅助，非 Profile 真源）。</summary>
    const int FailedAttackRetryFrames = 12;

    /// <summary>刷新假相机朝向的最小逻辑帧间隔（表现默认）。</summary>
    const int FacingRepathIntervalFrames = 6;

    readonly EnemyBrainProfile _profile;
    readonly EnemyPerception _perception;
    readonly Transform _facingProxy;
    readonly IEnemyBehaviorRunner _runner;
    readonly ActionEntryRequestBuffer _actionEntryRequests;
    readonly LocomotionDesireBuffer _locomotionDesires;
    readonly EnemyBlackboard _blackboard = new EnemyBlackboard();

    int _repathFramesRemaining;
    bool _awaitingAttackConfirm;
    bool _running = true;
    bool _debugEnabled;
    BehaviorStatus _lastRunnerStatus;
    string _lastDebugPath = string.Empty;
    string _lastCombatRequestEntryId = string.Empty;
    LocomotionDesire _lastLocomotionDesire;

    /// <summary>创建 BT 宿主；combat 关闭（木桩）时 runner 可为 null。</summary>
    public EnemyBrain(
        EnemyBrainProfile profile,
        EnemyPerception perception,
        Transform facingProxy,
        IEnemyBehaviorRunner runner,
        IEnemyPathQuery pathQuery = null,
        ActionEntryRequestBuffer actionEntryRequests = null,
        LocomotionDesireBuffer locomotionDesires = null)
    {
        _profile = profile;
        _perception = perception;
        _facingProxy = facingProxy;
        _runner = runner;
        _actionEntryRequests = actionEntryRequests;
        _locomotionDesires = locomotionDesires;
        _blackboard.PathQuery = pathQuery ?? new StraightPathQuery();
        State = EnemyBrainState.Idle;
    }

    /// <summary>调试用派生状态（门闩或黑板输出推断，非独立 FSM 决策）。</summary>
    public EnemyBrainState State { get; private set; }

    /// <summary>当前 Brain 是否仍参与决策。</summary>
    public bool IsRunning => _running;

    /// <summary>上一帧 Runner 根状态（无 Runner 时为 Failure）。</summary>
    public BehaviorStatus LastRunnerStatus => _lastRunnerStatus;

    /// <summary>上一帧 NamedNode 调试路径。</summary>
    public string LastDebugPath => _lastDebugPath;

    /// <summary>黑板仇恨滞回（调试；由 AggroGate 维护）。</summary>
    public bool DebugIsAggroed => _blackboard.IsAggroed;

    /// <summary>上一帧提交的 LocomotionDesire 本地轴（调试）。</summary>
    public Vector2 DebugMoveDesire => _lastLocomotionDesire.LocalMove;

    /// <summary>上一帧提交的 CombatRequest Entry（调试/HUD）。</summary>
    public string DebugCombatRequestEntryId => _lastCombatRequestEntryId;

    /// <summary>基础攻击成功冷却剩余帧（调试/测试）。</summary>
    public int DebugBasicAttackCooldownFrames =>
        _blackboard.Cooldowns.GetRemaining(EnemyCooldownIds.BasicAttack);

    /// <summary>Action Entry 请求失败重试冷却剩余帧（调试/测试）。</summary>
    public int DebugActionEntryRetryFrames =>
        _blackboard.Cooldowns.GetRemaining(EnemyCooldownIds.ActionEntryRetry);

    /// <summary>开关行为树路径采集（Gizmo/日志用）。</summary>
    public void SetDebugEnabled(bool enabled)
    {
        _debugEnabled = enabled;
        _blackboard.DebugEnabled = enabled;
    }

    /// <summary>基于上一帧已提交状态推进一次 AI 决策并提交 Desire/Request。</summary>
    public void Step()
    {
        if (!_running || _profile == null || _perception == null)
            return;

        _blackboard.Cooldowns.TickDown();
        _repathFramesRemaining = Mathf.Max(0, _repathFramesRemaining - 1);

        EnemyPerceptionSnapshot snapshot = _perception.Capture();
        if (snapshot.IsDead)
        {
            NotifyDeath();
            return;
        }

        // 木桩：禁止追打，但必须消化 Hit 门闩，否则硬直结束无法回 Idle
        if (!_profile.EnableCombatActions)
        {
            if (State == EnemyBrainState.Hit)
            {
                TickHitGate(in snapshot);
                return;
            }

            ClearCommandBuffers();
            State = EnemyBrainState.Idle;
            return;
        }

        if (State == EnemyBrainState.Hit)
        {
            TickHitGate(in snapshot);
            return;
        }

        if (_runner == null)
        {
            ClearCommandBuffers();
            State = EnemyBrainState.Idle;
            return;
        }

        ResolveAttackConfirm(in snapshot);
        FillBlackboard(in snapshot);
        _blackboard.DebugEnabled = _debugEnabled;
        _blackboard.ResetFrameOutputs();
        _lastRunnerStatus = _runner.Tick(_blackboard);
        _lastDebugPath = _debugEnabled ? _blackboard.DebugPath : string.Empty;
        CommitOutputs(in snapshot);
        DeriveDebugState();
    }

    /// <summary>生命值收到非致命伤害时立即抢占追击与攻击欲望。</summary>
    public void NotifyHit()
    {
        if (!_running || State == EnemyBrainState.Dead)
            return;

        ClearCommandBuffers();
        State = EnemyBrainState.Hit;
        _awaitingAttackConfirm = false;
        _blackboard.AttackConfirmPending = false;
        _lastCombatRequestEntryId = string.Empty;
        _runner?.Reset();
    }

    /// <summary>生命值归零时进入最高优先级死亡终态。</summary>
    public void NotifyDeath()
    {
        ClearCommandBuffers();
        State = EnemyBrainState.Dead;
        _running = false;
        _awaitingAttackConfirm = false;
        _blackboard.AttackConfirmPending = false;
        _lastCombatRequestEntryId = string.Empty;
        _runner?.Reset();
    }

    /// <summary>回收前停止决策并清空命令槽。</summary>
    public void Stop()
    {
        _running = false;
        ClearCommandBuffers();
        _runner?.Reset();
    }

    /// <summary>Hit 期间保持空命令，直到正式 Character Hit 状态结束。</summary>
    void TickHitGate(in EnemyPerceptionSnapshot snapshot)
    {
        ClearCommandBuffers();
        if (snapshot.CharacterState == CharacterStateType.Hit)
            return;

        _runner?.Reset();
        if (!_profile.EnableCombatActions)
        {
            State = EnemyBrainState.Idle;
            return;
        }

        // 仇恨由下次 Runner（AggroGate）刷新；出 Hit 后先回 Idle 调试态
        State = EnemyBrainState.Idle;
    }

    /// <summary>观测 CombatRequest 后是否真正进入 Action；确认/丢弃节点暂存 CD，失败另写重试槽。</summary>
    void ResolveAttackConfirm(in EnemyPerceptionSnapshot snapshot)
    {
        if (!_awaitingAttackConfirm)
            return;

        if (snapshot.CharacterState == CharacterStateType.Action)
        {
            _blackboard.Cooldowns.ConfirmPending();
            _awaitingAttackConfirm = false;
            return;
        }

        if (snapshot.CharacterState == CharacterStateType.Locomotion)
        {
            _blackboard.Cooldowns.DiscardPending();
            _blackboard.Cooldowns.Set(
                EnemyCooldownIds.ActionEntryRetry,
                FailedAttackRetryFrames);
            _awaitingAttackConfirm = false;
        }
    }

    /// <summary>把感知与确认旗位写入黑板（Runner 只读这些条件）。</summary>
    void FillBlackboard(in EnemyPerceptionSnapshot snapshot)
    {
        _blackboard.HasTarget = snapshot.HasTarget;
        _blackboard.PlanarDistance = snapshot.PlanarDistance;
        _blackboard.PlanarDirection = snapshot.PlanarDirection;
        _blackboard.CharacterState = snapshot.CharacterState;
        _blackboard.IsDead = snapshot.IsDead;
        _blackboard.AttackConfirmPending = _awaitingAttackConfirm;

        Vector3 path = snapshot.PlanarDirection;
        if (_blackboard.PathQuery != null && snapshot.HasTarget)
        {
            path = _blackboard.PathQuery.GetSteerDirection(
                Vector3.zero,
                snapshot.TargetPosition,
                snapshot.PlanarDirection);
            if (path.sqrMagnitude <= 0.0001f)
                path = snapshot.PlanarDirection;
        }

        _blackboard.PathDirection = path;
    }

    /// <summary>帧末提交 Desire + CombatRequest，并刷新 facing proxy。</summary>
    void CommitOutputs(in EnemyPerceptionSnapshot snapshot)
    {
        bool canSubmitRequest = _blackboard.HasCombatRequest
            && !string.IsNullOrEmpty(_blackboard.CombatRequestEntryId)
            && snapshot.CharacterState == CharacterStateType.Locomotion
            && _blackboard.Cooldowns.IsReady(EnemyCooldownIds.ActionEntryRetry);
        if (_blackboard.HasCombatRequest && !canSubmitRequest)
        {
            // 非 Locomotion 时 Driver 必然拒绝请求；同步丢弃节点暂存，禁止误认当前 Action 为本次起手。
            _blackboard.Cooldowns.DiscardPending();
        }
        // Action / 起手确认期强制清移动，避免 BT 装饰 Abort Wait 后 Strafe 污染攻击旋转
        bool freezeMove = snapshot.CharacterState == CharacterStateType.Action
            || _awaitingAttackConfirm
            || canSubmitRequest;

        Vector2 localMove = freezeMove ? Vector2.zero : _blackboard.MoveDesire;
        bool faceTarget = _blackboard.FaceTargetRequested
            || (!freezeMove && _blackboard.MoveDesire.sqrMagnitude > 0.0001f);
        _lastLocomotionDesire = new LocomotionDesire(localMove, faceTarget);
        if (_locomotionDesires != null)
            _locomotionDesires.Set(in _lastLocomotionDesire);

        _lastCombatRequestEntryId = string.Empty;
        if (canSubmitRequest)
        {
            _actionEntryRequests?.Set(new ActionEntryRequest(_blackboard.CombatRequestEntryId));
            _lastCombatRequestEntryId = _blackboard.CombatRequestEntryId;
            _awaitingAttackConfirm = true;
        }
        else
            _actionEntryRequests?.Clear();

        if (_lastLocomotionDesire.FaceTarget)
            RefreshFacingProxy(in snapshot);
    }

    /// <summary>由输出与仇恨推导调试状态（非决策真源）。</summary>
    void DeriveDebugState()
    {
        if (_awaitingAttackConfirm || _blackboard.HasCombatRequest)
            State = EnemyBrainState.Attack;
        else if (_blackboard.IsAggroed)
            State = EnemyBrainState.Chase;
        else
            State = EnemyBrainState.Idle;
    }

    /// <summary>门闩/停机时清空 Desire 与 CombatRequest。</summary>
    void ClearCommandBuffers()
    {
        _lastLocomotionDesire = LocomotionDesire.None;
        _locomotionDesires?.Clear();
        _actionEntryRequests?.Clear();
        _blackboard.Cooldowns.DiscardPending();
    }

    /// <summary>按表现默认间隔刷新假相机水平朝向。</summary>
    void RefreshFacingProxy(in EnemyPerceptionSnapshot snapshot)
    {
        if (_facingProxy == null || _repathFramesRemaining > 0)
            return;

        // FaceTarget 优先看向目标；PathDirection 仅作无 Planar 时回退
        Vector3 dir = _blackboard.FaceTargetRequested && snapshot.PlanarDirection.sqrMagnitude > 0.0001f
            ? snapshot.PlanarDirection
            : (_blackboard.PathDirection.sqrMagnitude > 0.0001f
                ? _blackboard.PathDirection
                : snapshot.PlanarDirection);
        if (dir.sqrMagnitude <= 0.0001f)
            return;

        _facingProxy.rotation = Quaternion.LookRotation(dir, Vector3.up);
        _repathFramesRemaining = FacingRepathIntervalFrames;
    }
}

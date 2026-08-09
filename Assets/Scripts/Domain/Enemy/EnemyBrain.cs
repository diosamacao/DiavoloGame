using UnityEngine;

/// <summary>
/// 敌人 AI 宿主：门闩 + 黑板填装 + IEnemyBehaviorRunner Tick + 帧末写 AIInputWriter。
/// 决策本身不在此类内 switch，便于替换 Runner 后端。
/// </summary>
public sealed class EnemyBrain
{
    readonly EnemyBrainProfile _profile;
    readonly EnemyPerception _perception;
    readonly AIInputWriter _input;
    readonly Transform _facingProxy;
    readonly IEnemyBehaviorRunner _runner;
    readonly EnemyBlackboard _blackboard = new EnemyBlackboard();

    int _repathFramesRemaining;
    bool _awaitingAttackConfirm;
    bool _running = true;
    bool _debugEnabled;
    BehaviorStatus _lastRunnerStatus;
    string _lastDebugPath = string.Empty;

    /// <summary>创建 BT 宿主；combat 关闭（木桩）时 runner 可为 null。</summary>
    public EnemyBrain(
        EnemyBrainProfile profile,
        EnemyPerception perception,
        AIInputWriter input,
        Transform facingProxy,
        IEnemyBehaviorRunner runner,
        IEnemyPathQuery pathQuery = null)
    {
        _profile = profile;
        _perception = perception;
        _input = input;
        _facingProxy = facingProxy;
        _runner = runner;
        _blackboard.Profile = profile;
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

    /// <summary>黑板仇恨滞回（调试）。</summary>
    public bool DebugIsAggroed => _blackboard.IsAggroed;

    /// <summary>上一帧提交的移动欲望（调试）。</summary>
    public Vector2 DebugMoveDesire => _blackboard.MoveDesire;

    /// <summary>上一帧是否发出攻击脉冲（调试）。</summary>
    public bool DebugAttackPulse => _blackboard.AttackPulse;

    /// <summary>开关行为树路径采集（Gizmo/日志用）。</summary>
    public void SetDebugEnabled(bool enabled)
    {
        _debugEnabled = enabled;
        _blackboard.DebugEnabled = enabled;
    }

    /// <summary>基于上一帧已提交状态推进一次 AI 决策并准备当前帧输入。</summary>
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

            _input.ClearAll();
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
            _input.ClearAll();
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

        _input.ClearAll();
        State = EnemyBrainState.Hit;
        _awaitingAttackConfirm = false;
        _blackboard.AttackConfirmPending = false;
        _runner?.Reset();
    }

    /// <summary>生命值归零时进入最高优先级死亡终态。</summary>
    public void NotifyDeath()
    {
        _input.ClearAll();
        State = EnemyBrainState.Dead;
        _running = false;
        _awaitingAttackConfirm = false;
        _blackboard.AttackConfirmPending = false;
        _runner?.Reset();
    }

    /// <summary>回收前停止决策并清空输入。</summary>
    public void Stop()
    {
        _running = false;
        _input.ClearAll();
        _runner?.Reset();
    }

    /// <summary>Hit 期间保持空输入，直到正式 Character Hit 状态结束。</summary>
    void TickHitGate(in EnemyPerceptionSnapshot snapshot)
    {
        _input.ClearAll();
        if (snapshot.CharacterState == CharacterStateType.Hit)
            return;

        _runner?.Reset();
        if (!_profile.EnableCombatActions)
        {
            State = EnemyBrainState.Idle;
            return;
        }

        UpdateAggro(in snapshot);
        State = _blackboard.IsAggroed ? EnemyBrainState.Chase : EnemyBrainState.Idle;
    }

    /// <summary>观测 Pulse 后是否真正进入 Action，以维护 basic_attack 冷却。</summary>
    void ResolveAttackConfirm(in EnemyPerceptionSnapshot snapshot)
    {
        if (!_awaitingAttackConfirm)
            return;

        if (snapshot.CharacterState == CharacterStateType.Action)
        {
            _awaitingAttackConfirm = false;
            _blackboard.Cooldowns.Set(EnemyCooldownIds.BasicAttack, _profile.AttackCooldownFrames);
            return;
        }

        if (snapshot.CharacterState == CharacterStateType.Locomotion)
        {
            _awaitingAttackConfirm = false;
            _blackboard.Cooldowns.Set(EnemyCooldownIds.BasicAttack, _profile.FailedAttackRetryFrames);
        }
    }

    /// <summary>把感知与确认旗位写入黑板（Runner 只读这些条件）。</summary>
    void FillBlackboard(in EnemyPerceptionSnapshot snapshot)
    {
        _blackboard.Profile = _profile;
        _blackboard.HasTarget = snapshot.HasTarget;
        _blackboard.PlanarDistance = snapshot.PlanarDistance;
        _blackboard.PlanarDirection = snapshot.PlanarDirection;
        _blackboard.CharacterState = snapshot.CharacterState;
        _blackboard.IsDead = snapshot.IsDead;
        _blackboard.AttackConfirmPending = _awaitingAttackConfirm;
        UpdateAggro(in snapshot);

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

    /// <summary>维护进战/脱战滞回旗位。</summary>
    void UpdateAggro(in EnemyPerceptionSnapshot snapshot)
    {
        if (!snapshot.HasTarget)
        {
            _blackboard.IsAggroed = false;
            return;
        }

        if (!_blackboard.IsAggroed && snapshot.PlanarDistance <= _profile.AggroRadius)
            _blackboard.IsAggroed = true;
        else if (_blackboard.IsAggroed && snapshot.PlanarDistance > _profile.LoseAggroRadius)
            _blackboard.IsAggroed = false;
    }

    /// <summary>帧末把黑板输出提交到 AIInputWriter，并刷新 facing proxy。</summary>
    void CommitOutputs(in EnemyPerceptionSnapshot snapshot)
    {
        // Action / 起手确认期强制清移动，避免 BT 装饰 Abort Wait 后 Strafe 污染攻击旋转
        bool freezeMove = snapshot.CharacterState == CharacterStateType.Action
            || _awaitingAttackConfirm
            || _blackboard.AttackPulse;
        _input.SetMove(freezeMove ? Vector2.zero : _blackboard.MoveDesire);

        if (_blackboard.AttackPulse)
        {
            if (_input.PulseAttack())
                _awaitingAttackConfirm = true;
            else
                _blackboard.Cooldowns.Set(EnemyCooldownIds.BasicAttack, _profile.FailedAttackRetryFrames);
        }

        if (_blackboard.DodgePulse)
            _input.PulseDodge();

        if (_blackboard.HeavyAttackPulse)
            _input.Pulse(InputButton.HeavyAttack);

        if (_blackboard.SkillPulse)
            _input.Pulse(InputButton.Skill);

        if (_blackboard.FaceTargetRequested
            || (!freezeMove && _blackboard.MoveDesire.sqrMagnitude > 0.0001f))
        {
            RefreshFacingProxy(in snapshot);
        }
    }

    /// <summary>由输出与仇恨推导调试状态（非决策真源）。</summary>
    void DeriveDebugState()
    {
        if (_awaitingAttackConfirm || _blackboard.AttackPulse)
            State = EnemyBrainState.Attack;
        else if (_blackboard.IsAggroed)
            State = EnemyBrainState.Chase;
        else
            State = EnemyBrainState.Idle;
    }

    /// <summary>按配置间隔刷新假相机水平朝向。</summary>
    void RefreshFacingProxy(in EnemyPerceptionSnapshot snapshot)
    {
        if (!_profile.FaceTargetWhileChase
            || _facingProxy == null
            || _repathFramesRemaining > 0)
        {
            return;
        }

        // FaceTarget 优先看向目标；PathDirection 仅作无 Planar 时回退（避免 Strafe 残留追击朝向）
        Vector3 dir = _blackboard.FaceTargetRequested && snapshot.PlanarDirection.sqrMagnitude > 0.0001f
            ? snapshot.PlanarDirection
            : (_blackboard.PathDirection.sqrMagnitude > 0.0001f
                ? _blackboard.PathDirection
                : snapshot.PlanarDirection);
        if (dir.sqrMagnitude <= 0.0001f)
            return;

        _facingProxy.rotation = Quaternion.LookRotation(dir, Vector3.up);
        _repathFramesRemaining = _profile.RepathIntervalFrames;
    }
}

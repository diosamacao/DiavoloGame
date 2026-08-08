using UnityEngine;

/// <summary>敌人五态决策器；只写 AI 输入，不直接选择或启动 Action。</summary>
public sealed class EnemyBrain
{
    readonly EnemyBrainProfile _profile;
    readonly EnemyPerception _perception;
    readonly AIInputWriter _input;
    readonly Transform _facingProxy;

    int _attackCooldownFramesRemaining;
    int _repathFramesRemaining;
    bool _attackStarted;
    bool _running = true;

    /// <summary>创建 Idle/Chase/Attack/Hit/Dead 决策器。</summary>
    public EnemyBrain(
        EnemyBrainProfile profile,
        EnemyPerception perception,
        AIInputWriter input,
        Transform facingProxy)
    {
        _profile = profile;
        _perception = perception;
        _input = input;
        _facingProxy = facingProxy;
        State = EnemyBrainState.Idle;
    }

    /// <summary>当前 AI 状态。</summary>
    public EnemyBrainState State { get; private set; }

    /// <summary>当前 Brain 是否仍参与决策。</summary>
    public bool IsRunning => _running;

    /// <summary>基于上一帧已提交状态推进一次 AI 决策并准备当前帧输入。</summary>
    public void Step()
    {
        if (!_running || _profile == null || _perception == null)
            return;

        _attackCooldownFramesRemaining = Mathf.Max(0, _attackCooldownFramesRemaining - 1);
        _repathFramesRemaining = Mathf.Max(0, _repathFramesRemaining - 1);

        EnemyPerceptionSnapshot snapshot = _perception.Capture();
        if (snapshot.IsDead)
        {
            NotifyDeath();
            return;
        }

        // 木桩：禁止 Chase/Attack，但必须继续 TickHit，否则硬直结束无法回 Idle
        if (!_profile.EnableCombatActions)
        {
            if (State == EnemyBrainState.Hit)
            {
                TickHit(in snapshot);
                return;
            }

            _input.ClearAll();
            State = EnemyBrainState.Idle;
            return;
        }

        switch (State)
        {
            case EnemyBrainState.Idle:
                TickIdle(in snapshot);
                break;
            case EnemyBrainState.Chase:
                TickChase(in snapshot);
                break;
            case EnemyBrainState.Attack:
                TickAttack(in snapshot);
                break;
            case EnemyBrainState.Hit:
                TickHit(in snapshot);
                break;
        }
    }

    /// <summary>生命值收到非致命伤害时立即抢占追击与攻击欲望。</summary>
    public void NotifyHit()
    {
        if (!_running || State == EnemyBrainState.Dead)
            return;

        _input.ClearAll();
        State = EnemyBrainState.Hit;
        _attackStarted = false;
    }

    /// <summary>生命值归零时进入最高优先级死亡终态。</summary>
    public void NotifyDeath()
    {
        _input.ClearAll();
        State = EnemyBrainState.Dead;
        _running = false;
        _attackStarted = false;
    }

    /// <summary>回收前停止决策并清空输入。</summary>
    public void Stop()
    {
        _running = false;
        _input.ClearAll();
    }

    /// <summary>Idle 不输出移动；目标进入仇恨圈后开始追击。</summary>
    void TickIdle(in EnemyPerceptionSnapshot snapshot)
    {
        _input.SetMove(Vector2.zero);
        if (snapshot.HasTarget && snapshot.PlanarDistance <= _profile.AggroRadius)
            State = EnemyBrainState.Chase;
    }

    /// <summary>Chase 用假相机把局部前进映射到目标方向，并在距离与冷却满足时发攻击脉冲。</summary>
    void TickChase(in EnemyPerceptionSnapshot snapshot)
    {
        if (!snapshot.HasTarget || snapshot.PlanarDistance > _profile.LoseAggroRadius)
        {
            _input.SetMove(Vector2.zero);
            State = EnemyBrainState.Idle;
            return;
        }

        RefreshFacingProxy(in snapshot);
        bool shouldStop = snapshot.PlanarDistance <= _profile.StopDistance;
        _input.SetMove(shouldStop ? Vector2.zero : Vector2.up * _profile.ChaseMoveMagnitude);

        if (snapshot.PlanarDistance > _profile.AttackRange
            || _attackCooldownFramesRemaining > 0
            || snapshot.CharacterState != CharacterStateType.Locomotion)
        {
            return;
        }

        _input.SetMove(Vector2.zero);
        if (_input.PulseAttack())
        {
            State = EnemyBrainState.Attack;
            _attackStarted = false;
        }
        else
        {
            _attackCooldownFramesRemaining = _profile.FailedAttackRetryFrames;
        }
    }

    /// <summary>Attack 等待角色管线确认起手并在 Action 完成后恢复 Chase/Idle。</summary>
    void TickAttack(in EnemyPerceptionSnapshot snapshot)
    {
        _input.SetMove(Vector2.zero);
        if (snapshot.CharacterState == CharacterStateType.Action)
        {
            if (!_attackStarted)
            {
                _attackStarted = true;
                _attackCooldownFramesRemaining = _profile.AttackCooldownFrames;
            }

            return;
        }

        if (!_attackStarted)
            _attackCooldownFramesRemaining = _profile.FailedAttackRetryFrames;

        State = HasAggro(in snapshot) ? EnemyBrainState.Chase : EnemyBrainState.Idle;
    }

    /// <summary>Hit 期间保持空输入，直到正式 Character Hit 状态结束。</summary>
    void TickHit(in EnemyPerceptionSnapshot snapshot)
    {
        _input.ClearAll();
        if (snapshot.CharacterState == CharacterStateType.Hit)
            return;

        // 木桩硬直结束后不得因距离误入 Chase
        if (!_profile.EnableCombatActions)
        {
            State = EnemyBrainState.Idle;
            return;
        }

        State = HasAggro(in snapshot) ? EnemyBrainState.Chase : EnemyBrainState.Idle;
    }

    /// <summary>按配置间隔刷新假相机水平朝向，减少不必要的 Transform 写入。</summary>
    void RefreshFacingProxy(in EnemyPerceptionSnapshot snapshot)
    {
        if (!_profile.FaceTargetWhileChase
            || _facingProxy == null
            || _repathFramesRemaining > 0
            || snapshot.PlanarDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _facingProxy.rotation = Quaternion.LookRotation(snapshot.PlanarDirection, Vector3.up);
        _repathFramesRemaining = _profile.RepathIntervalFrames;
    }

    /// <summary>目标仍在脱战半径内即保持仇恨。</summary>
    bool HasAggro(in EnemyPerceptionSnapshot snapshot) =>
        snapshot.HasTarget && snapshot.PlanarDistance <= _profile.LoseAggroRadius;
}

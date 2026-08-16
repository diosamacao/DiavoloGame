using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 远端角色表现体：应用 Snapshot 位姿、播 Clip、过点派发 VFX/SFX。
/// 客机可作只读 <see cref="ITargetable"/>；OnHit 空操作，禁止 Collect。
/// </summary>
public sealed class RemoteCharacterProxy : IDisposable, ICharacterFacingDebugTarget, ITargetable
{
    readonly Transform _root;
    readonly CharacterMotor _motor;
    readonly CharacterAnimationService _animation;
    readonly CharacterPresentationBridge _presentation;
    readonly Transform _visualMotionRoot;
    readonly CharacterVisualMotionBridge _visualMotion;
    readonly ActionReplicationCatalog _catalog;
    readonly IActionNotifyConsumer[] _notifyConsumers;
    readonly Vector3 _worldOffset;
    readonly float _fixedDeltaSeconds;
    readonly bool _ownsRoot;

    ActionDefinition _animationAction;
    int _animationSegmentIndex = -1;
    int _lastActionId;
    int _lastActionFrame;
    AnimationKey? _locomotionKey;
    bool _visualActionActive;
    Vector3 _debugWishWorld;
    SimActorId _simulationId;
    int _teamId;
    int _healthMilli;
    readonly HurtboxDefinition _hurtbox;

    /// <summary>幽灵权威根；调试与测试读位姿用。</summary>
    public Transform Root => _root;

    /// <summary>幽灵逻辑电机；仅被快照写入，不进 SimulationWorld。</summary>
    public CharacterMotorSim MotorSim => _motor.Sim;

    /// <summary>供相机以外的表现跟随的插值锚点。</summary>
    public Transform PresentationRoot => _presentation != null ? _presentation.PresentationRoot : _root;

    /// <summary>本机/幽灵当前快照正在播招或受击残差。</summary>
    public bool IsPresentingAction => _lastActionId != 0 || _visualActionActive;

    /// <summary>幽灵永不收集命中；恒为 false，供装配断言。</summary>
    public bool CollectsHits => false;

    /// <inheritdoc />
    public SimActorId SimulationId => _simulationId;

    /// <inheritdoc />
    public Transform TargetTransform => _root;

    /// <inheritdoc />
    public Transform AimTransform => PresentationRoot != null ? PresentationRoot : _root;

    /// <inheritdoc />
    public bool IsAlive =>
        _root != null
        && _root.gameObject.activeInHierarchy
        && _healthMilli > 0;

    /// <inheritdoc />
    public float CurrentHealth => _healthMilli / 1000f;

    /// <inheritdoc />
    public int TeamId => _teamId;

    /// <inheritdoc />
    public bool HasFacingDebugPose => _presentation != null;

    /// <inheritdoc />
    public Vector3 FacingDebugFeetWorld =>
        _presentation != null ? _presentation.RenderedPosition : _root.position;

    /// <inheritdoc />
    public Vector3 FacingDebugWishWorld => _debugWishWorld;

    /// <inheritdoc />
    public Vector3 FacingDebugModelForward
    {
        get
        {
            if (_visualMotionRoot != null)
                return _visualMotionRoot.forward;
            if (_presentation?.PresentationRoot != null)
                return _presentation.PresentationRoot.forward;
            return _root != null ? _root.forward : Vector3.forward;
        }
    }

    /// <summary>装配已创建的表现图；animation / visualMotionRoot / notifyConsumers 可空（仅测位姿时）。</summary>
    public RemoteCharacterProxy(
        Transform root,
        CharacterMotor motor,
        CharacterAnimationService animation,
        CharacterPresentationBridge presentation,
        ActionReplicationCatalog catalog,
        Vector3 worldOffset,
        float fixedDeltaSeconds,
        Transform visualMotionRoot = null,
        bool ownsRoot = false,
        IReadOnlyList<IActionNotifyConsumer> notifyConsumers = null,
        HurtboxDefinition hurtbox = null)
    {
        _root = root != null ? root : throw new ArgumentNullException(nameof(root));
        _hurtbox = hurtbox ?? new HurtboxDefinition();
        _motor = motor ?? throw new ArgumentNullException(nameof(motor));
        _animation = animation;
        _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        _visualMotionRoot = visualMotionRoot;
        _visualMotion = visualMotionRoot != null
            ? new CharacterVisualMotionBridge(visualMotionRoot)
            : null;
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _notifyConsumers = CopyConsumers(notifyConsumers);
        _worldOffset = worldOffset;
        _fixedDeltaSeconds = fixedDeltaSeconds > 0f
            ? fixedDeltaSeconds
            : 1f / SimulationConfig.DefaultLogicHz;
        _ownsRoot = ownsRoot;
    }

    /// <summary>
    /// 应用一帧权威快照：写 Motor、同步根、Seek/播 Locomotion；不 Dispatch 判定帧。
    /// leanRollDegrees 仅预测预览传入（Lean 不进 Snapshot）；幽灵默认 0。
    /// seekLocomotion=false 时走跑只 Tick，避免本机预测每逻辑帧 Seek 抽帧。
    /// </summary>
    public void ApplySnapshot(
        in ActorReplicationSnapshot snapshot,
        float leanRollDegrees = 0f,
        bool seekLocomotion = true)
    {
        BindReplicationIdentity(in snapshot);
        _presentation.BeginSimulationStep();
        ReplicationPoseApplier.ApplyToMotor(_motor.Sim, in snapshot);
        ApplyWorldOffset();
        _motor.SyncRootPoseFromSim();
        ApplyDebugWish(in snapshot);
        ApplyPresentation(in snapshot, leanRollDegrees, seekLocomotion);
        _presentation.EndSimulationStep();
    }

    /// <summary>
    /// 本机走跑：只同步位置与 Lean，禁止 Play/Seek Locomotion（片子由 Runner 推进）。
    /// 不调用 SyncRootPoseFromSim，以免清零转向阻尼。预览偏移只写进表现采样。
    /// </summary>
    public void SyncAutonomousLocomotion(float leanRollDegrees, Vector3 debugWishWorld)
    {
        _presentation.BeginSimulationStep();
        int savedX = _motor.Sim.PositionMm.X;
        int savedY = _motor.Sim.YMm;
        int savedZ = _motor.Sim.PositionMm.Z;
        int savedFacing = _motor.Sim.FacingMilliDeg;
        ApplyWorldOffset();
        // 只同步位置，保留 ApplyLocomotion 的 SmoothDamp 朝向与转向速度
        _motor.SyncRootFromSim();
        if (_worldOffset.sqrMagnitude >= 0.0001f)
        {
            _motor.Sim.TeleportMm(savedX, savedY, savedZ);
            _motor.Sim.SetFacingMilliDeg(savedFacing);
        }

        _debugWishWorld = debugWishWorld;
        _locomotionKey = _animation != null ? _animation.CurrentKey : null;
        _animationAction = null;
        _animationSegmentIndex = -1;
        _lastActionId = 0;
        _lastActionFrame = 0;
        _visualMotion?.SetLeanRollDegrees(leanRollDegrees);
        if (_visualActionActive)
        {
            _visualMotion?.EndAction(VisualResidualExitPolicy.BlendToZero);
            _visualActionActive = false;
        }

        _presentation.EndSimulationStep();
    }

    /// <summary>纠偏吸附后立刻对齐表现锚点，禁止插值扫过回拉。</summary>
    public void SnapPresentationToSimulation() => _presentation.SnapToSimulationRoot();

    /// <summary>逻辑根 Pose；索敌用 MotorSim，不读表现骨骼。</summary>
    public SimCombatPose GetLogicalCombatPose()
    {
        float heightY = _root != null ? _root.position.y : 0f;
        return SimCombatPose.FromMotor(_motor.Sim, heightY);
    }

    /// <summary>只读几何，供接口完整；客机不得拿去 Collect。</summary>
    public HitboxOrientedBox GetLogicalHurtbox()
    {
        SimCombatPose pose = GetLogicalCombatPose();
        return HitboxMath.BuildFromHurtboxLogical(in pose, _hurtbox);
    }

    /// <summary>只读目标：命中不写血、不进 Pipeline。</summary>
    public void OnHit(in ActionHitContext context)
    {
    }

    /// <summary>按 Host 插值比例更新模型锚点与视觉残差/倾身；邻近 Pose 走 lerp，禁止每渲染帧硬切。</summary>
    public void Render(float interpolationAlpha)
    {
        // 快照前后 Pose 插值到表现锚点
        _presentation.Render(interpolationAlpha);
        // 视觉残差 / 倾身跟渲染帧，不回写 Motor
        _visualMotion?.Render(interpolationAlpha, Time.deltaTime);
    }

    /// <summary>释放动画后端；ownsRoot 时销毁幽灵根物体。</summary>
    public void Dispose()
    {
        _animation?.Dispose();
        if (_ownsRoot && _root != null)
            UnityEngine.Object.Destroy(_root.gameObject);
    }

    /// <summary>从快照写入索敌身份；不改变表现图。</summary>
    void BindReplicationIdentity(in ActorReplicationSnapshot snapshot)
    {
        _simulationId = snapshot.ActorId;
        _teamId = snapshot.TeamId;
        _healthMilli = snapshot.HealthMilli;
    }

    /// <summary>预览偏移加在毫米坐标上，避免与 Host 模型重叠。</summary>
    void ApplyWorldOffset()
    {
        if (_worldOffset.sqrMagnitude < 0.0001f)
            return;

        int x = _motor.Sim.PositionMm.X + MotionQuantization.MetersToMm(_worldOffset.x);
        int y = _motor.Sim.YMm + MotionQuantization.MetersToMm(_worldOffset.y);
        int z = _motor.Sim.PositionMm.Z + MotionQuantization.MetersToMm(_worldOffset.z);
        _motor.Sim.TeleportMm(x, y, z);
    }

    /// <summary>从同 Tick 的 moveV* 还原 wish，保证幽灵黄箭与延迟位姿成对。</summary>
    void ApplyDebugWish(in ActorReplicationSnapshot snapshot)
    {
        _debugWishWorld = new Vector3(
            MotionQuantization.MmToMeters(snapshot.MoveVxMm),
            0f,
            MotionQuantization.MmToMeters(snapshot.MoveVzMm));
    }

    /// <summary>
    /// 有招则切段时 Play+Seek，并按跨帧规则只派发 VFX/SFX。
    /// 走跑切键后 Tick，禁止每帧 Seek。禁止派发 Hitbox。
    /// </summary>
    void ApplyPresentation(
        in ActorReplicationSnapshot snapshot,
        float leanRollDegrees,
        bool seekLocomotion)
    {
        bool frozen = snapshot.FreezeFrames > 0;
        // 先声明再 out：短路时编译器不认为 action 已赋值（CS0165）
        ActionDefinition action = null;
        bool actionActive = snapshot.ActionId != 0
            && _catalog.TryGet(snapshot.ActionId, out action);
        bool forceRestart = ShouldForceActionRestart(
            snapshot.VitalityEdge,
            _lastActionId,
            _lastActionFrame,
            snapshot.ActionId,
            snapshot.ActionFrame);
        int previousActionFrame = !forceRestart && _lastActionId == snapshot.ActionId
            ? _lastActionFrame
            : -1;

        if (_animation != null)
        {
            if (actionActive)
            {
                _locomotionKey = null;
                SeekActionIfSegmentChanged(action, snapshot.ActionFrame, forceRestart);
                _animation.SetSpeed(frozen ? 0f : 1f);
                if (!frozen)
                    _animation.Tick(_fixedDeltaSeconds);
            }
            else
            {
                _animationAction = null;
                _animationSegmentIndex = -1;
                _animation.SetSpeed(frozen ? 0f : 1f);
                AnimationKey key = ResolveLocomotionKey(snapshot.LocomotionPhase);
                bool keyChanged = _locomotionKey != key;
                if (keyChanged)
                {
                    bool hardCut = ReplicationPresentationAlign.ShouldHardCut(_locomotionKey, key);
                    _animation.Play(key, hardCut ? 0f : (float?)null);
                    _locomotionKey = key;
                    // 一次性相位才 Seek 对齐权威时间；走跑循环淡入后只 Tick
                    if (seekLocomotion
                        && !frozen
                        && ReplicationPresentationAlign.IsTransitionPhase(key))
                        _animation.SeekLocomotionNormalized(snapshot.LocomotionNormalizedMilli / 1000f);
                }

                // 同键只 Tick：远端也不再每 Tick Seek，避免抽帧
                if (!frozen && !keyChanged)
                    _animation.Tick(_fixedDeltaSeconds);
            }
        }

        if (actionActive && !frozen)
            DispatchPresentationNotifies(action, previousActionFrame, snapshot.ActionFrame);

        _lastActionId = snapshot.ActionId;
        _lastActionFrame = snapshot.ActionFrame;

        // 倾身先写入，残差贴帧会一并带上 localRotation
        _visualMotion?.SetLeanRollDegrees(leanRollDegrees);
        if (actionActive)
        {
            _visualMotion?.CaptureSimulationFrame(action, snapshot.ActionFrame, actionActive: true);
            _visualActionActive = true;
        }
        else if (_visualActionActive)
        {
            _visualMotion?.EndAction(VisualResidualExitPolicy.BlendToZero);
            _visualActionActive = false;
        }
    }

    /// <summary>
    /// 同动作同段默认不 Seek，避免每逻辑帧硬切。
    /// 连续受击须 forceRestart：与权威 EnterHit(force) 一样从头播。
    /// </summary>
    void SeekActionIfSegmentChanged(ActionDefinition action, int actionFrame, bool forceRestart)
    {
        ActionFrameQueryResult query = ActionFrameQuery.Query(action, actionFrame);
        if (!query.HasAnimationSegment)
            return;

        int segmentIndex = query.SegmentIndex;
        if (!forceRestart && _animationAction == action && _animationSegmentIndex == segmentIndex)
            return;

        ActionAnimationSegment segment = query.Segment;
        // 连击重播必须 fade=0，否则和上一段受击混在一起看不出重入
        float fade = forceRestart ? 0f : action.ResolveSegmentCrossFade(segmentIndex);
        _animation.PlayClip(segment.clip, fade);
        _animation.SeekClip(query.SegmentLocalTime);
        _animationAction = action;
        _animationSegmentIndex = segmentIndex;
    }

    /// <summary>
    /// 生命边沿 Hit/Death，或同一招动作帧回绕（再次 EnterHit），需要硬切重播。
    /// </summary>
    public static bool ShouldForceActionRestart(
        VitalityReplicationEdge edge,
        int previousActionId,
        int previousActionFrame,
        int actionId,
        int actionFrame)
    {
        if (edge == VitalityReplicationEdge.Hit || edge == VitalityReplicationEdge.Death)
            return true;

        return actionId != 0
            && actionId == previousActionId
            && actionFrame < previousActionFrame;
    }

    /// <summary>只把 VFX/SFX 点事件交给消费者；不调用 notify.OnNotify，也不派发 Motion/Hitbox。</summary>
    void DispatchPresentationNotifies(ActionDefinition action, int previousFrame, int currentFrame)
    {
        if (action?.Timeline == null || _notifyConsumers.Length == 0)
            return;

        IReadOnlyList<ActionNotify> notifies = action.Timeline.GetTriggeredNotifies(
            previousFrame,
            currentFrame);
        for (int i = 0; i < notifies.Count; i++)
        {
            ActionNotify notify = notifies[i];
            if (!IsPresentationNotify(notify))
                continue;

            var context = new ActionNotifyContext(
                action,
                currentFrame,
                previousFrame,
                _root,
                _root,
                notify);
            for (int c = 0; c < _notifyConsumers.Length; c++)
                _notifyConsumers[c].OnActionNotify(in context);
        }
    }

    /// <summary>表现向点事件：刀光与动作音效。位移/判定命令不得走 Proxy。</summary>
    public static bool IsPresentationNotify(ActionNotify notify) =>
        notify is PlayVfxNotify || notify is PlaySfxNotify;

    static IActionNotifyConsumer[] CopyConsumers(IReadOnlyList<IActionNotifyConsumer> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<IActionNotifyConsumer>();

        var copy = new IActionNotifyConsumer[source.Count];
        for (int i = 0; i < source.Count; i++)
            copy[i] = source[i];
        return copy;
    }

    static AnimationKey ResolveLocomotionKey(byte locomotionPhase)
    {
        if (!Enum.IsDefined(typeof(AnimationKey), (int)locomotionPhase))
            return AnimationKey.Idle;
        return (AnimationKey)locomotionPhase;
    }
}

using System;
using UnityEngine;

/// <summary>
/// 远端角色表现体：只应用 Snapshot 位姿、动作 Seek、视觉残差/倾身；不跑动作模拟核、命中收集或敌人 AI。
/// </summary>
public sealed class RemoteCharacterProxy : IDisposable, ICharacterFacingDebugTarget
{
    readonly Transform _root;
    readonly CharacterMotor _motor;
    readonly CharacterAnimationService _animation;
    readonly CharacterPresentationBridge _presentation;
    readonly Transform _visualMotionRoot;
    readonly CharacterVisualMotionBridge _visualMotion;
    readonly ActionReplicationCatalog _catalog;
    readonly Vector3 _worldOffset;
    readonly float _fixedDeltaSeconds;
    readonly bool _ownsRoot;

    ActionDefinition _animationAction;
    int _animationSegmentIndex = -1;
    AnimationKey? _locomotionKey;
    bool _visualActionActive;
    Vector3 _debugWishWorld;

    /// <summary>幽灵权威根；调试与测试读位姿用。</summary>
    public Transform Root => _root;

    /// <summary>幽灵逻辑电机；仅被快照写入，不进 SimulationWorld。</summary>
    public CharacterMotorSim MotorSim => _motor.Sim;

    /// <summary>供相机以外的表现跟随的插值锚点。</summary>
    public Transform PresentationRoot => _presentation != null ? _presentation.PresentationRoot : _root;

    /// <summary>幽灵永不收集命中；恒为 false，供装配断言。</summary>
    public bool CollectsHits => false;

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

    /// <summary>装配已创建的表现图；animation / visualMotionRoot 可空（仅测位姿时）。</summary>
    public RemoteCharacterProxy(
        Transform root,
        CharacterMotor motor,
        CharacterAnimationService animation,
        CharacterPresentationBridge presentation,
        ActionReplicationCatalog catalog,
        Vector3 worldOffset,
        float fixedDeltaSeconds,
        Transform visualMotionRoot = null,
        bool ownsRoot = false)
    {
        _root = root != null ? root : throw new ArgumentNullException(nameof(root));
        _motor = motor ?? throw new ArgumentNullException(nameof(motor));
        _animation = animation;
        _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        _visualMotionRoot = visualMotionRoot;
        _visualMotion = visualMotionRoot != null
            ? new CharacterVisualMotionBridge(visualMotionRoot)
            : null;
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _worldOffset = worldOffset;
        _fixedDeltaSeconds = fixedDeltaSeconds > 0f
            ? fixedDeltaSeconds
            : 1f / SimulationConfig.DefaultLogicHz;
        _ownsRoot = ownsRoot;
    }

    /// <summary>
    /// 应用一帧权威快照：写 Motor、同步根、Seek/播 Locomotion；不 Dispatch 判定帧。
    /// leanRollDegrees 仅预测预览传入（Lean 不进 Snapshot）；幽灵默认 0。
    /// </summary>
    public void ApplySnapshot(in ActorReplicationSnapshot snapshot, float leanRollDegrees = 0f)
    {
        _presentation.BeginSimulationStep();
        ReplicationPoseApplier.ApplyToMotor(_motor.Sim, in snapshot);
        ApplyWorldOffset();
        _motor.SyncRootPoseFromSim();
        ApplyDebugWish(in snapshot);
        ApplyPresentation(in snapshot, leanRollDegrees);
        _presentation.EndSimulationStep();
    }

    /// <summary>按 Host 插值比例更新模型锚点与视觉残差/倾身；邻近 Pose 走 lerp，禁止每渲染帧硬切。</summary>
    public void Render(float interpolationAlpha)
    {
        _presentation.Render(interpolationAlpha);
        _visualMotion?.Render(interpolationAlpha, Time.deltaTime);
    }

    /// <summary>释放动画后端；ownsRoot 时销毁幽灵根物体。</summary>
    public void Dispose()
    {
        _animation?.Dispose();
        if (_ownsRoot && _root != null)
            UnityEngine.Object.Destroy(_root.gameObject);
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

    /// <summary>有招则只在切段时 Play+Seek；空闲播 LocomotionKey。禁止派发 Hitbox。</summary>
    void ApplyPresentation(in ActorReplicationSnapshot snapshot, float leanRollDegrees)
    {
        bool frozen = snapshot.FreezeFrames > 0;
        // 先声明再 out：短路时编译器不认为 action 已赋值（CS0165）
        ActionDefinition action = null;
        bool actionActive = snapshot.ActionId != 0
            && _catalog.TryGet(snapshot.ActionId, out action);

        if (_animation != null)
        {
            if (actionActive)
            {
                _locomotionKey = null;
                SeekActionIfSegmentChanged(action, snapshot.ActionFrame);
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
                if (_locomotionKey != key)
                {
                    // 与 PivotTurn Enter 相同：硬切，禁止和上一 Gait CrossFade 把转身混花
                    _animation.Play(key, 0f);
                    _locomotionKey = key;
                }

                // 对齐权威归一化时间；Seek 已 Evaluate，勿再 Tick 以免超前一帧
                if (!frozen)
                    _animation.SeekLocomotionNormalized(snapshot.LocomotionNormalizedMilli / 1000f);
            }
        }

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

    /// <summary>与权威表现桥相同：同动作同段不 Seek，避免每逻辑帧硬切。</summary>
    void SeekActionIfSegmentChanged(ActionDefinition action, int actionFrame)
    {
        ActionFrameQueryResult query = ActionFrameQuery.Query(action, actionFrame);
        if (!query.HasAnimationSegment)
            return;

        int segmentIndex = query.SegmentIndex;
        if (_animationAction == action && _animationSegmentIndex == segmentIndex)
            return;

        ActionAnimationSegment segment = query.Segment;
        _animation.PlayClip(segment.clip, action.ResolveSegmentCrossFade(segmentIndex));
        _animation.SeekClip(query.SegmentLocalTime);
        _animationAction = action;
        _animationSegmentIndex = segmentIndex;
    }

    static AnimationKey ResolveLocomotionKey(byte locomotionPhase)
    {
        if (!Enum.IsDefined(typeof(AnimationKey), (int)locomotionPhase))
            return AnimationKey.Idle;
        return (AnimationKey)locomotionPhase;
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>纯托管窗口边界，覆盖时间轴查找、间隙与输入释放。</summary>
public sealed class ActionInputMovementWindowTests
{
    [TestCase(9, false)]
    [TestCase(10, true)]
    [TestCase(20, true)]
    [TestCase(21, false)]
    public void Window_UsesInclusiveTimelineFrames(int frame, bool active)
    {
        var window = Window(10, 20);
        Assert.That(window.IsActiveAtFrame(frame), Is.EqualTo(active));
    }

    [Test]
    public void Timeline_SelectsWindowAndLeavesGapInactive()
    {
        var timeline = new ActionTimeline();
        var first = Window(0, 5);
        var second = Window(7, 9);
        typeof(ActionTimeline).GetField("inputMovementStates", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(timeline, new[] { first, second });
        Assert.That(timeline.GetActiveInputMovementAtFrame(5), Is.SameAs(first));
        Assert.That(timeline.GetActiveInputMovementAtFrame(6), Is.Null);
        Assert.That(timeline.GetActiveInputMovementAtFrame(7), Is.SameAs(second));
    }

    [Test]
    public void Release_BypassesDirectionMinimum()
    {
        var window = Window(0, 10);
        int state = window.AdvanceState(ActionInputMoveState.Pack(1, 5), 6, 0);
        Assert.That(ActionInputMoveState.Cardinal(state), Is.Zero);
        Assert.That(ActionInputMoveState.StartFrame(state), Is.EqualTo(6));
    }

    static ActionInputMovement Window(int start, int end)
    {
        var window = new ActionInputMovement { overrideMovementAnimation = false };
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(ActionTimelineItem).GetField("startFrame", flags).SetValue(window, start);
        typeof(ActionTimelineItem).GetField("endFrame", flags).SetValue(window, end);
        return window;
    }
}

/// <summary>动作内移动必须保留同一实例与帧时钟，并复用碰撞和方向播放。</summary>
public sealed class ActionInputMovementTests
{
    GameObject root;
    ActionDefinition action;
    AnimationClip clip;
    InputManager input;
    CharacterMotor motor;
    ActionSim sim;
    CharacterActionGameplayStep gameplay;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("InputMovementTest");
        clip = new AnimationClip();
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 0));
        action = ScriptableObject.CreateInstance<ActionDefinition>();
        using (var so = new SerializedObject(action))
        {
            so.FindProperty("animationSegments").arraySize = 1;
            var segment = so.FindProperty("animationSegments").GetArrayElementAtIndex(0);
            segment.FindPropertyRelative("clip").objectReferenceValue = clip;
            segment.FindPropertyRelative("endFrame").intValue = -1;
            so.FindProperty("totalFrames").intValue = 60;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        action.ExecutionPolicy.EditorSetBaseMotionMode(ActionBaseMotionMode.InputMovement);
        AddMovementWindow(0, 59, true);
        ActionInputMovement move = action.Timeline.InputMovementStates[0];
        move.forward = move.back = move.left = move.right = clip;
        move.speedMmPerSecond = 6000;
        input = new InputManager();
        motor = new CharacterMotor(root.transform, null, CharacterMotorConfig.Default, input);
        sim = new ActionSim();
        gameplay = new CharacterActionGameplayStep(sim, root.transform, motor, null, null);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(action);
        Object.DestroyImmediate(clip);
        Object.DestroyImmediate(root);
    }

    [TestCase(0, 127, 0, 100, 1)]
    [TestCase(0, -127, 0, -100, 2)]
    [TestCase(-127, 0, -100, 0, 3)]
    [TestCase(127, 0, 100, 0, 4)]
    public void Direction_UsesInputWithoutRotatingOrRestarting(int x, int y, int dx, int dz, int cardinal)
    {
        SetInput(x, y);
        Assert.That(sim.TryStart(ActionSimResolveResult.FromContent(action)), Is.True);
        int instance = sim.InstanceId;
        gameplay.ApplyStep(1f / 60, null);
        Assert.That(motor.Sim.PositionMm.X, Is.EqualTo(dx));
        Assert.That(motor.Sim.PositionMm.Z, Is.EqualTo(dz));
        Assert.That(ActionInputMoveState.Cardinal(gameplay.InputMovementState), Is.EqualTo(cardinal));
        sim.Step();
        gameplay.ApplyStep(1f / 60, null);
        Assert.That(sim.InstanceId, Is.EqualTo(instance));
        Assert.That(sim.CurrentFrame, Is.EqualTo(1));
        Assert.That(motor.Sim.FacingMilliDeg, Is.Zero);
    }

    [Test]
    public void DiagonalAndReferenceYaw_UseNormalizedWorldInput()
    {
        SetInput(127, 127);
        sim.TryStart(ActionSimResolveResult.FromContent(action));
        gameplay.ApplyStep(1f / 60, null);
        Assert.That(motor.Sim.PositionMm.X, Is.EqualTo(71));
        Assert.That(motor.Sim.PositionMm.Z, Is.EqualTo(71));
        SetInput(0, 127, 900);
        sim.Step(); gameplay.ApplyStep(1f / 60, null);
        Assert.That(motor.Sim.PositionMm.X, Is.EqualTo(171));
        Assert.That(motor.Sim.PositionMm.Z, Is.EqualTo(71));
    }

    [Test]
    public void ReleaseAndHitStop_DoNotMoveOrRestartClock_IncludingLastFrozenTick()
    {
        SetInput(0, 127);
        sim.TryStart(ActionSimResolveResult.FromContent(action));
        gameplay.ApplyStep(1f / 60, null);
        int instance = sim.InstanceId;
        sim.RequestHitStop(instance, 2, false);
        for (int i = 0; i < 2; i++)
        {
            sim.Step(); gameplay.ApplyStep(1f / 60, null);
            Assert.That(motor.Sim.PositionMm.Z, Is.EqualTo(100));
            Assert.That(sim.CurrentFrame, Is.Zero);
        }
        SetInput(0, 0);
        sim.Step(); gameplay.ApplyStep(1f / 60, null);
        Assert.That(motor.Sim.PositionMm.Z, Is.EqualTo(100));
        Assert.That(ActionInputMoveState.Cardinal(gameplay.InputMovementState), Is.Zero);
        Assert.That(sim.InstanceId, Is.EqualTo(instance));
        for (int i = 1; i < action.TotalFrames; i++)
        { sim.Step(); gameplay.ApplyStep(1f / 60, null); }
        Assert.That(sim.IsComplete, Is.True);
        sim.ResolvePostCombat(); gameplay.ApplyPostCombat(null);
        Assert.That(sim.IsActive, Is.False);
        Assert.That(gameplay.InputMovementState, Is.Zero);
    }

    [Test]
    public void BodyCollision_BlocksTranslationWithoutClearingMovePose()
    {
        using (var so = new SerializedObject(action))
        {
            so.FindProperty("executionPolicy.bodyCollisionMode").intValue = (int)ActionBodyCollisionMode.StopOnContact;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        motor.BindBodyObstacles(new BodyQuery(), () => new SimActorId(1));
        SetInput(0, 127);
        sim.TryStart(ActionSimResolveResult.FromContent(action));
        gameplay.ApplyStep(1f / 60, null);
        for (int i = 0; i < 8; i++) { sim.Step(); gameplay.ApplyStep(1f / 60, null); }
        Assert.That(motor.Sim.PositionMm.Z, Is.InRange(419, 420));
        Assert.That(ActionInputMoveState.Cardinal(gameplay.InputMovementState), Is.EqualTo(1));
        Assert.That(gameplay.LastInputMovementCommand.Delta.Z, Is.EqualTo(100));
    }

    [Test]
    public void DirectionHysteresisAndClipTime_AreBoundToActionFrames()
    {
        var move = action.Timeline.InputMovementStates[0];
        move.animationTimeMode = ActionInputMovement.AnimationTimeMode.FromDirectionChange;
        int state = move.AdvanceState(0, 7, 1);
        Assert.That(move.AdvanceState(state, 8, 4), Is.EqualTo(state));
        state = move.AdvanceState(state, 10, 4);
        Assert.That(ActionInputMoveState.StartFrame(state), Is.EqualTo(10));
        Assert.That(move.SampleTime(action, state, 100), Is.EqualTo(.5f).Within(.001));
        move.loopMove = false;
        Assert.That(move.SampleTime(action, state, 100), Is.EqualTo(59f / 60).Within(.001));
        state = move.AdvanceState(state, 11, 0);
        Assert.That(ActionInputMoveState.Cardinal(state), Is.Zero);
        Assert.That(move.SampleTime(action, state, 11), Is.Zero);
    }

    [Test]
    public void MissingClipAndMovementCancel_AreRejectedBeforeEnteringAction()
    {
        action.Timeline.InputMovementStates[0].left = null;
        Assert.That(sim.TryStart(ActionSimResolveResult.FromContent(action)), Is.False);
        action.Timeline.InputMovementStates[0].left = clip;
        using (var so = new SerializedObject(action))
        {
            var phases = so.FindProperty("timeline.phaseStates");
            phases.arraySize = 1;
            phases.GetArrayElementAtIndex(0).FindPropertyRelative("kind").intValue = (int)ActionPhaseKind.Recovery;
            phases.GetArrayElementAtIndex(0).FindPropertyRelative("allowMovementCancel").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        Assert.That(action.GetInputMovementError(), Does.Contain("AllowMovementCancel"));
    }

    [Test]
    public void Playback_AdvancesBlendButSeeksClock_AndDoesNotReplayOnDuplicateState()
    {
        var playback = new RecordingPlayback();
        var animation = new CharacterAnimationService(playback, null, null);
        var player = new ActionInputMovementPlayer();
        int state = ActionInputMoveState.Pack(1, 10);
        player.Sample(animation, action, 10, state, deltaTime: 1f / 60);
        player.Sample(animation, action, 11, state, deltaTime: 1f / 60);
        Assert.That(playback.Plays, Is.EqualTo(1));
        Assert.That(playback.Ticks, Is.EqualTo(2));
        Assert.That(playback.Time, Is.EqualTo(11f / 60).Within(.001));
        player.Reset(); player.Sample(animation, action, 0, 0);
        Assert.That(playback.Plays, Is.EqualTo(2));
    }

    [Test]
    public void SynchronizedMovement_ClockGroupSurvivesDirectionAndReleaseButChangesAtSegmentBoundary()
    {
        ConfigureTrimmedPoseAndCancel();
        var movement = action.Timeline.InputMovementStates[0];
        SetRange(movement, 18, 60);
        var playback = new RecordingPlayback();
        var animation = new CharacterAnimationService(playback, null, null);
        var player = new ActionInputMovementPlayer();
        player.Sample(animation, action, 18, ActionInputMoveState.Pack(3, 18));
        object clock = playback.TimeGroup;
        Assert.That(clock, Is.Not.Null);
        player.Sample(animation, action, 20, ActionInputMoveState.Pack(4, 20));
        Assert.That(playback.TimeGroup, Is.SameAs(clock));
        player.Sample(animation, action, 21, ActionInputMoveState.Pack(0, 21));
        Assert.That(playback.TimeGroup, Is.SameAs(clock));
        player.Sample(animation, action, 42, ActionInputMoveState.Pack(0, 21));
        Assert.That(playback.TimeGroup, Is.Not.SameAs(clock));
        object cancelClock = playback.TimeGroup;
        player.Sample(animation, action, 43, ActionInputMoveState.Pack(0, 21), restart: true);
        Assert.That(playback.TimeGroup, Is.Not.SameAs(cancelClock));
        movement.animationTimeMode = ActionInputMovement.AnimationTimeMode.FromDirectionChange;
        player.Sample(animation, action, 44, ActionInputMoveState.Pack(1, 44));
        Assert.That(playback.TimeGroup, Is.Null);
    }

    [Test]
    public void SynchronizedMovement_UsesTrimmedSegmentClockThroughDirectionChangesAndRelease()
    {
        ConfigureTrimmedPoseAndCancel();
        var movement = action.Timeline.InputMovementStates[0];
        SetRange(movement, 18, 41); // 窗口在 Pose 中途开启，不能以窗口起点计时。
        movement.loopMove = true; // 同步模式必须忽略独立循环配置。
        var playback = new RecordingPlayback();
        var animation = new CharacterAnimationService(playback, null, null);
        var player = new ActionInputMovementPlayer();
        player.Sample(animation, action, 18, ActionInputMoveState.Pack(1, 18));
        Assert.That(playback.Time, Is.EqualTo(12f / 60).Within(.0001));
        player.Sample(animation, action, 30.5f, ActionInputMoveState.Pack(3, 30));
        Assert.That(playback.Time, Is.EqualTo(24.5f / 60).Within(.0001));
        player.Sample(animation, action, 31, ActionInputMoveState.Pack(0, 31));
        Assert.That(playback.Time, Is.EqualTo(25f / 60).Within(.0001));
        player.Sample(animation, action, 41, ActionInputMoveState.Pack(4, 41));
        Assert.That(playback.Time, Is.EqualTo(35f / 60).Within(.0001));
        Assert.That(action.IsInputMovementAnimationActive(42), Is.False);
        var cancel = ActionFrameQuery.Query(action, 42);
        Assert.That(cancel.SegmentIndex, Is.EqualTo(2));
        Assert.That(cancel.SegmentFrameOffset, Is.Zero);
        Assert.That(cancel.SegmentLocalTime, Is.Zero);
    }

    [Test]
    public void SynchronizedMovement_IgnoresDirectionStartAndDoesNotWrapAtClipEnd()
    {
        var movement = action.Timeline.InputMovementStates[0];
        foreach (int direction in new[] { 1, 2, 3, 4 })
        {
            Assert.That(movement.SampleTime(action, ActionInputMoveState.Pack(direction, 59), 59),
                Is.EqualTo(59f / 60).Within(.0001));
            Assert.That(movement.SampleTime(action, ActionInputMoveState.Pack(direction, 59), 59.5f),
                Is.EqualTo(ActionInputMovement.SampleActionSegmentTime(action, 59.5f)).Within(.0001));
            Assert.That(movement.SampleTime(action, ActionInputMoveState.Pack(direction, 0), 60),
                Is.EqualTo(59f / 60).Within(.0001));
        }
    }

    void ConfigureTrimmedPoseAndCancel()
    {
        using (var so = new SerializedObject(action))
        {
            var segments = so.FindProperty("animationSegments");
            segments.arraySize = 3;
            int[] starts = { 0, 6, 0 }, ends = { 11, 35, 23 };
            for (int i = 0; i < 3; i++)
            {
                var segment = segments.GetArrayElementAtIndex(i);
                segment.FindPropertyRelative("clip").objectReferenceValue = clip;
                segment.FindPropertyRelative("startFrame").intValue = starts[i];
                segment.FindPropertyRelative("endFrame").intValue = ends[i];
            }
            so.FindProperty("totalFrames").intValue = 66;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    [Test]
    public void HeldInput_AutoTransitionsAtOriginalEnd_AndPaysOncePerAction()
    {
        var cancel = Object.Instantiate(action);
        cancel.ExecutionPolicy.EditorSetBaseMotionMode(ActionBaseMotionMode.None);
        typeof(ActionTimeline).GetField("inputMovementStates", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(cancel.Timeline, new ActionInputMovement[0]);
        try
        {
            var gate = new CountingGate();
            sim = new ActionSim(resourceGate: gate);
            gameplay = new CharacterActionGameplayStep(sim, root.transform, motor, null, null);
            SetInput(0, 127);
            sim.TryStart(ActionSimResolveResult.FromGraph(action, new EndGraph(cancel), "pose"));
            int instance = sim.InstanceId;
            gameplay.ApplyStep(1f / 60, null);
            for (int frame = 1; frame <= action.TotalFrames; frame++)
            {
                sim.Step(); gameplay.ApplyStep(1f / 60, null);
                sim.ResolvePostCombat(); gameplay.ApplyPostCombat(null);
                Assert.That(sim.InstanceId, Is.EqualTo(instance));
                Assert.That(gate.Costs, Is.EqualTo(1));
            }
            SimVec2 end = motor.Sim.PositionMm;
            sim.Step(); gameplay.ApplyStep(1f / 60, null);
            Assert.That(sim.Snapshot.Content, Is.SameAs(cancel));
            Assert.That(sim.CurrentFrame, Is.Zero);
            Assert.That(gate.Costs, Is.EqualTo(2));
            Assert.That(motor.Sim.PositionMm, Is.EqualTo(end));
            Assert.That(gameplay.InputMovementState, Is.Zero);
        }
        finally { Object.DestroyImmediate(cancel); }
    }

    [Test]
    public void Observer_DoesNotSwitchDirectionBeforeReplicatedStartFrame()
    {
        var from = Snapshot(10, ActionInputMoveState.Pack(1, 5));
        var to = Snapshot(16, ActionInputMoveState.Pack(4, 13));
        Assert.That(RemoteCharacterProxy.ResolveInputMovementState(in from, in to, 12), Is.EqualTo(from.ActionMovementState));
        Assert.That(RemoteCharacterProxy.ResolveInputMovementState(in from, in to, 13), Is.EqualTo(to.ActionMovementState));
    }

    [TestCase(0)]
    [TestCase(2)]
    public void MovementWindowEnd_ResumesBaseClipAndMovesOnlyThroughTail(int tailFrames)
    {
        var moveClip = Object.Instantiate(clip);
        try
        {
            action.Timeline.InputMovementStates[0].forward = moveClip;
            SetRange(action.Timeline.InputMovementStates[0], 0, 1);
            if (tailFrames > 0) AddMovementWindow(2, 1 + tailFrames, false);
            var playback = new RecordingPlayback();
            var animation = new CharacterAnimationService(playback, null, null);
            var sink = new CharacterActionPresentationBridge(root.transform, animation, null, null, null, null);
            SetInput(0, 127);
            sim.TryStart(ActionSimResolveResult.FromContent(action));
            int instance = sim.InstanceId;
            gameplay.ApplyStep(1f / 60, sink);
            Assert.That(playback.CurrentClip, Is.SameAs(moveClip));
            sim.Step(); gameplay.ApplyStep(1f / 60, sink);
            sim.Step(); gameplay.ApplyStep(1f / 60, sink);
            Assert.That(sim.CurrentFrame, Is.EqualTo(2));
            Assert.That(sim.IsActive, Is.True);
            Assert.That(playback.CurrentClip, Is.SameAs(clip));
            Assert.That(motor.Sim.PositionMm.Z, Is.EqualTo(tailFrames == 0 ? 200 : 300));
            Assert.That(gameplay.LastInputMovementCommand.IsPresent, Is.EqualTo(tailFrames > 0));
            while (sim.CurrentFrame < 4) { sim.Step(); gameplay.ApplyStep(1f / 60, sink); }
            Assert.That(motor.Sim.PositionMm.Z, Is.EqualTo(200 + tailFrames * 100));
            Assert.That(gameplay.LastInputMovementCommand.IsPresent, Is.False);
            Assert.That(playback.CurrentClip, Is.SameAs(clip));
            Assert.That(sim.InstanceId, Is.EqualTo(instance));
        }
        finally { Object.DestroyImmediate(moveClip); }
    }

    [TestCase(3, true)]
    [TestCase(4, false)]
    public void MovementTail_RejectsOverlappingMovementCancel(int recoveryStart, bool rejected)
    {
        SetRange(action.Timeline.InputMovementStates[0], 0, 1);
        AddMovementWindow(2, 3, false);
        using (var so = new SerializedObject(action))
        {
            var phases = so.FindProperty("timeline.phaseStates");
            phases.arraySize = 1;
            var phase = phases.GetArrayElementAtIndex(0);
            phase.FindPropertyRelative("kind").intValue = (int)ActionPhaseKind.Recovery;
            phase.FindPropertyRelative("startFrame").intValue = recoveryStart;
            phase.FindPropertyRelative("endFrame").intValue = 59;
            phase.FindPropertyRelative("allowMovementCancel").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        Assert.That(action.GetInputMovementError() != null, Is.EqualTo(rejected));
    }

    [Test]
    public void Windows_RejectOverlapAndOutOfBounds()
    {
        SetRange(action.Timeline.InputMovementStates[0], 0, 2);
        var other = AddMovementWindow(2, 4, false);
        Assert.That(action.GetInputMovementError(), Does.Contain("重叠"));
        SetRange(other, 3, 4);
        Assert.That(action.IsSimulationReady, Is.True);
        SetRange(other, 3, 60);
        Assert.That(action.IsSimulationReady, Is.False);
    }

    [Test]
    public void Release_SeeksOriginalActionTimeInsteadOfRestartingPose()
    {
        var playback = new RecordingPlayback();
        var player = new ActionInputMovementPlayer();
        var animation = new CharacterAnimationService(playback, null, null);
        player.Sample(animation, action, 10, ActionInputMoveState.Pack(1, 5));
        player.Sample(animation, action, 30, ActionInputMoveState.Pack(0, 30));
        Assert.That(playback.CurrentClip, Is.SameAs(clip));
        Assert.That(playback.Time, Is.EqualTo(.5f).Within(.001));
    }

    [Test]
    public void TrackEditor_CreatesAndDeletesRealWindow()
    {
        using (var so = new SerializedObject(action))
        {
            so.FindProperty("timeline.inputMovementStates").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            ActionTimelineCommands.AddTrack(so, ActionTimelineTrackKind.InputMovement);
            string name = action.Timeline.Tracks[0].TrackName;
            ActionTimelineCommands.AddWindow(so, ActionTimelineTrackKind.InputMovement, name, 10, 60, 60);
            Assert.That(action.Timeline.InputMovementStates.Length, Is.EqualTo(1));
            Assert.That(action.Timeline.InputMovementStates[0].StartFrame, Is.EqualTo(10));
            Assert.That(action.ExecutionPolicy.UsesInputMovement, Is.True);
            ActionTimelineCommands.RemoveTrack(so, 0);
            Assert.That(action.Timeline.InputMovementStates, Is.Empty);
        }
    }

    ActionInputMovement AddMovementWindow(int start, int end, bool animation)
    {
        var window = new ActionInputMovement { overrideMovementAnimation = animation, speedMmPerSecond = 6000 };
        SetRange(window, start, end);
        var windows = new List<ActionInputMovement>(action.Timeline.InputMovementStates) { window };
        typeof(ActionTimeline).GetField("inputMovementStates", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(action.Timeline, windows.ToArray());
        return window;
    }

    static void SetRange(ActionInputMovement window, int start, int end)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(ActionTimelineItem).GetField("startFrame", flags).SetValue(window, start);
        typeof(ActionTimelineItem).GetField("endFrame", flags).SetValue(window, end);
    }
    static ActorReplicationSnapshot Snapshot(int frame, int state) =>
        new ActorReplicationSnapshot(new SimActorId(1), 1, ReplicationActorKind.Player,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, frame, 0, SimActorId.Invalid, 1000, 0, VitalityReplicationEdge.None,
            actionMovementState: state);

    sealed class CountingGate : IActionResourceGate
    {
        public int Costs;
        public bool CanAfford(IActionSimContent content) => true;
        public void CommitCost(IActionSimContent content) { Costs++; }
    }

    sealed class EndGraph : IActionSimGraph
    {
        readonly ActionDefinition cancel;
        public EndGraph(ActionDefinition cancel) { this.cancel = cancel; }
        public void CollectCancelCandidateIntents(string node, CancelWindowType window, ISet<GameplayIntentType> results) { }
        public bool TryResolveAutomaticTransition(string node, IActionSimContent content, int frame, bool hit,
            out ActionSimResolveResult result, out bool stop)
        { result = ActionSimResolveResult.FromContent(cancel); stop = false; return frame >= content.TotalFrames; }
    }

    void SetInput(int x, int y, ushort yaw = 0) => input.IngestFrame(
        new InputFrame(1, new SimActorId(1), (sbyte)x, (sbyte)y, 0, 0, 0, yaw));

    sealed class BodyQuery : ISimBodyObstacleQuery
    {
        public void Collect(SimActorId id, List<SimBodyObstacle> results)
        { results.Clear(); results.Add(new SimBodyObstacle(new SimActorId(2), new SimVec2(0, 1000), 280)); }
    }

    sealed class RecordingPlayback : IAnimationPlayback
    {
        public bool IsValid => true;
        public float Speed { get; set; } = 1;
        public AnimationClip CurrentClip { get; private set; }
        public float NormalizedTime => Time;
        public bool HasFinished => false;
        public float AdditiveWeight => 0;
        public int Plays, Ticks;
        public float Time;
        public object TimeGroup;
        public void Play(AnimationClip clip, float fade, object timeGroup = null) { CurrentClip = clip; Plays++; TimeGroup = timeGroup; }
        public void Seek(float seconds) { Time = seconds; }
        public void Tick(float dt) { Ticks++; Time += dt; }
        public void PlayAdditive(AnimationClip clip, AvatarMask mask, float fade) { }
        public void StopAdditive() { }
        public void Dispose() { }
    }
}

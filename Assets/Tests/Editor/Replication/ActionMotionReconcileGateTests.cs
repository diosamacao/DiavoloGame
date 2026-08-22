using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>穿敌吸附/关碰撞/闪避整段与权威卡肉时，客机纠偏不得硬吸位姿。</summary>
public sealed class ActionMotionReconcileGateTests
{
    /// <summary>走跑空闲：走默认 2m 阈。</summary>
    [Test]
    public void Idle_UsesDefaultThreshold()
    {
        ActorReplicationSnapshot idle = CreateSnapshot(actionId: 0, actionFrame: 0, freezeFrames: 0);
        Assert.That(
            ActionMotionReconcileGate.ShouldDeferLocomotionSnap(
                false, null, 0, in idle, null),
            Is.False);
        Assert.That(
            ActionMotionReconcileGate.ResolveSnapThresholdMm(
                false, null, 0, in idle, null),
            Is.EqualTo(-1));
    }

    /// <summary>权威卡肉：即使本机已过窗也不硬吸。</summary>
    [Test]
    public void AuthorityFreeze_DefersSnap()
    {
        ActorReplicationSnapshot frozen = CreateSnapshot(actionId: 7, actionFrame: 25, freezeFrames: 3);
        Assert.That(
            ActionMotionReconcileGate.ShouldDeferLocomotionSnap(
                false, null, 40, in frozen, null),
            Is.True);
        Assert.That(
            ActionMotionReconcileGate.ResolveSnapThresholdMm(
                false, null, 40, in frozen, null),
            Is.EqualTo(int.MaxValue));
    }

    /// <summary>本机 SoftBody 抑制计数未清：不硬吸。</summary>
    [Test]
    public void LocalSoftBodySuppress_DefersSnap()
    {
        ActorReplicationSnapshot idle = CreateSnapshot(actionId: 0, actionFrame: 0, freezeFrames: 0);
        Assert.That(
            ActionMotionReconcileGate.ShouldDeferLocomotionSnap(
                true, null, 0, in idle, null),
            Is.True);
    }

    /// <summary>本机帧落在 SoftBodySuppress 窗内：不硬吸。</summary>
    [Test]
    public void LocalSuppressWindow_DefersSnap()
    {
        ActionDefinition action = CreateActionWithModifier(
            MotionModifierMode.SoftBodySuppress, 23, 32);
        try
        {
            ActorReplicationSnapshot idle = CreateSnapshot(actionId: 0, actionFrame: 0, freezeFrames: 0);
            Assert.That(
                ActionMotionReconcileGate.HasPassThroughWindow(action, 25),
                Is.True);
            Assert.That(
                ActionMotionReconcileGate.HasPassThroughWindow(action, 10),
                Is.False);
            Assert.That(
                ActionMotionReconcileGate.ShouldDeferLocomotionSnap(
                    false, action, 25, in idle, null),
                Is.True);
            // 整段招都推迟：窗前帧也不得 2m 硬吸。
            Assert.That(
                ActionMotionReconcileGate.ShouldDeferLocomotionSnap(
                    false, action, 10, in idle, null),
                Is.True);
        }
        finally
        {
            Object.DestroyImmediate(action);
        }
    }

    /// <summary>本机或权威正在 Dodge：连续闪避不得 2m 硬吸拉回。</summary>
    [Test]
    public void Dodge_DefersSnap()
    {
        ActionDefinition dodge = ScriptableObject.CreateInstance<ActionDefinition>();
        try
        {
            SetField(typeof(ActionDefinition), dodge, "actionType", CombatActionType.Dodge);
            ActorReplicationSnapshot idle = CreateSnapshot(actionId: 0, actionFrame: 0, freezeFrames: 0);
            Assert.That(
                ActionMotionReconcileGate.ShouldDeferLocomotionSnap(
                    false, dodge, 4, in idle, null),
                Is.True);

            ActorReplicationSnapshot authorityDodge = CreateSnapshot(
                actionId: 5, actionFrame: 6, freezeFrames: 0);
            Assert.That(
                ActionMotionReconcileGate.ShouldDeferLocomotionSnap(
                    false, null, 0, in authorityDodge, dodge),
                Is.True);
            Assert.That(
                ActionMotionReconcileGate.ResolveSnapThresholdMm(
                    false, dodge, 4, in idle, null),
                Is.EqualTo(int.MaxValue));
        }
        finally
        {
            Object.DestroyImmediate(dodge);
        }
    }

    /// <summary>权威帧仍在 TargetAdhesion 窗：本机已出窗也不硬吸。</summary>
    [Test]
    public void AuthorityAdhesionWindow_DefersSnap()
    {
        ActionDefinition action = CreateActionWithModifier(
            MotionModifierMode.TargetAdhesion, 0, 17);
        try
        {
            ActorReplicationSnapshot midDash = CreateSnapshot(
                actionId: 3, actionFrame: 10, freezeFrames: 0);
            Assert.That(
                ActionMotionReconcileGate.ShouldDeferLocomotionSnap(
                    false, null, 40, in midDash, action),
                Is.True);
            ActorReplicationSnapshot afterDash = CreateSnapshot(
                actionId: 3, actionFrame: 20, freezeFrames: 0);
            Assert.That(
                ActionMotionReconcileGate.ShouldDeferLocomotionSnap(
                    false, null, 40, in afterDash, action),
                Is.True);
            ActorReplicationSnapshot finished = CreateSnapshot(
                actionId: 0, actionFrame: 0, freezeFrames: 0);
            Assert.That(
                ActionMotionReconcileGate.ShouldDeferLocomotionSnap(
                    false, null, 0, in finished, action),
                Is.False);
        }
        finally
        {
            Object.DestroyImmediate(action);
        }
    }

    /// <summary>创建不经过资产 OnValidate 裁剪的时间轴窗口测试夹具。</summary>
    static ActionDefinition CreateActionWithModifier(
        MotionModifierMode mode,
        int startFrame,
        int endFrame)
    {
        ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
        var modifier = new MotionModifierNotifyState();
        SetField(typeof(ActionTimelineItem), modifier, "startFrame", startFrame);
        SetField(typeof(ActionTimelineItem), modifier, "endFrame", endFrame);
        SetField(typeof(MotionModifierNotifyState), modifier, "mode", mode);
        SetField(
            typeof(ActionTimeline),
            action.Timeline,
            "motionModifierStates",
            new[] { modifier });
        return action;
    }

    /// <summary>
    /// 直接构造 Gate 所需的纯时间轴夹具，避免无动画测试 Action 的 OnValidate
    /// 把所有窗口裁剪到第 0 帧。
    /// </summary>
    static void SetField<T>(System.Type ownerType, object target, string fieldName, T value)
    {
        FieldInfo field = ownerType.GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"测试夹具字段不存在：{ownerType.Name}.{fieldName}");
        field.SetValue(target, value);
    }

    static ActorReplicationSnapshot CreateSnapshot(int actionId, int actionFrame, int freezeFrames) =>
        new ActorReplicationSnapshot(
            new SimActorId(1),
            1,
            ReplicationActorKind.Player,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            actionId,
            0,
            actionFrame,
            freezeFrames,
            SimActorId.Invalid,
            100000,
            0,
            VitalityReplicationEdge.None);
}

using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>穿敌吸附/关碰撞窗与权威卡肉时，客机纠偏不得硬吸位姿。</summary>
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
                ActionMotionReconcileGate.ShouldDeferLocomotionSnap(
                    false, action, 25, in idle, null),
                Is.True);
            Assert.That(
                ActionMotionReconcileGate.ShouldDeferLocomotionSnap(
                    false, action, 10, in idle, null),
                Is.False);
        }
        finally
        {
            Object.DestroyImmediate(action);
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
                Is.False);
        }
        finally
        {
            Object.DestroyImmediate(action);
        }
    }

    static ActionDefinition CreateActionWithModifier(
        MotionModifierMode mode,
        int startFrame,
        int endFrame)
    {
        ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
        SerializedObject serialized = new SerializedObject(action);
        SerializedProperty modifiers = serialized
            .FindProperty("timeline")
            .FindPropertyRelative("motionModifierStates");
        modifiers.arraySize = 1;
        SerializedProperty element = modifiers.GetArrayElementAtIndex(0);
        element.FindPropertyRelative("startFrame").intValue = startFrame;
        element.FindPropertyRelative("endFrame").intValue = endFrame;
        element.FindPropertyRelative("mode").enumValueIndex = (int)mode;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return action;
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
            string.Empty,
            actionFrame,
            freezeFrames,
            SimActorId.Invalid,
            100000,
            0,
            VitalityReplicationEdge.None);
}

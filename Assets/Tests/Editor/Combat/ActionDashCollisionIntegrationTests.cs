using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>验证真实 GameplayStep 的单次提交、持续动作帧及 Editor 预览，不创建资产。</summary>
public sealed class ActionDashCollisionIntegrationTests
{
    [TestCase(ActionBodyCollisionMode.StopOnContact, 420)]
    [TestCase(ActionBodyCollisionMode.SoftSeparationOnly, 2226)]
    public void GameplayStep_UsesPolicy_AndKeepsAdvancing(ActionBodyCollisionMode mode, int expected)
    {
        var root = new GameObject("DashTest");
        var action = MakeAction(mode, out AnimationClip clip);
        try
        {
            var motor = new CharacterMotor(root.transform, null, CharacterMotorConfig.Default, new InputManager());
            var query = new BodyQuery();
            motor.BindBodyObstacles(query, () => new SimActorId(1));
            var sim = new ActionSim();
            var gameplay = new CharacterActionGameplayStep(sim, root.transform, motor, null, null);
            Assert.That(sim.TryStart(ActionSimResolveResult.FromContent(action)), Is.True);
            gameplay.ApplyStep(1f / 60, null);
            Assert.That(motor.Sim.PositionMm.Z, Is.EqualTo(expected));
            sim.Step();
            gameplay.ApplyStep(1f / 60, null);
            Assert.That(sim.CurrentFrame, Is.EqualTo(1));
            Assert.That(sim.IsActive, Is.True);
            if (mode == ActionBodyCollisionMode.StopOnContact)
            {
                Assert.That(motor.Sim.PositionMm.Z, Is.EqualTo(420));
                query.Bodies.Clear();
                sim.Step();
                gameplay.ApplyStep(1f / 60, null);
                Assert.That(motor.Sim.PositionMm.Z, Is.EqualTo(520));
            }
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(action); Object.DestroyImmediate(clip); }
    }

    [Test]
    public void AdhesionFinalFrame_CannotCrossBody_AndPreviewMatchesMotor()
    {
        var action = MakeAction(ActionBodyCollisionMode.StopOnContact, out AnimationClip clip);
        try
        {
            var window = new ActionMotionAdhesionParams(0, 1, 1000, 0, 10000, 10000, 0);
            var state = new ActionMotionAdhesion.State();
            var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, 280);
            var bodies = new[] { new SimBodyObstacle(new SimActorId(2), new SimVec2(0, 1000), 280) };
            for (int frame = 0; frame <= 1; frame++)
            {
                action.BakedMotion.TryGetDelta(frame, out SimVec2 delta, out _);
                ActionMotionAdhesion.TryComputeDisplacementMm(ref state, motor.PositionMm.X, motor.PositionMm.Z,
                    0, 0, 1000, in window, frame, delta.X, delta.Z,
                    ActionMotionAdhesion.BakedProgress(action.BakedMotion, frame, 1), out int x, out int z);
                motor.TryMoveActionMm(new SimVec2(x, z), 20, bodies, out _, out _);
            }
            ActionMotionAdhesionSceneDrawing.SimulateThroughFrame(action, in window, Vector3.zero,
                Quaternion.identity, Vector3.forward, 0, 1, null, out Vector3 preview, out _, out _);
            Assert.That(motor.PositionMm.Z, Is.InRange(419, 420));
            Assert.That(preview.z, Is.EqualTo(motor.PositionMm.Z / 1000f).Within(.0001));
        }
        finally { Object.DestroyImmediate(action); Object.DestroyImmediate(clip); }
    }

    static ActionDefinition MakeAction(ActionBodyCollisionMode mode, out AnimationClip clip)
    {
        var action = ScriptableObject.CreateInstance<ActionDefinition>();
        clip = new AnimationClip();
        clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 4f / 60, 0));
        using (var so = new SerializedObject(action))
        {
            so.FindProperty("totalFrames").intValue = 4;
            so.FindProperty("animationSegments").arraySize = 1;
            so.FindProperty("animationSegments").GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue = clip;
            so.FindProperty("animationSegments").GetArrayElementAtIndex(0).FindPropertyRelative("endFrame").intValue = -1;
            so.FindProperty("executionPolicy.baseMotionMode").intValue = (int)ActionBaseMotionMode.BakedMotion;
            so.FindProperty("executionPolicy.bodyCollisionMode").intValue = (int)mode;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        action.BakedMotion.CopyFrom(new ActionBakedMotion
        {
            frameCount = 4, bakeStatus = ActionBakedMotionStatus.Ok,
            positionDeltaMmX = new int[4], positionDeltaMmZ = new[] { 2226, 1943, 100, 0 },
            yawDeltaMilliDeg = new int[4]
        });
        return action;
    }

    sealed class BodyQuery : ISimBodyObstacleQuery
    {
        public readonly List<SimBodyObstacle> Bodies = new()
        { new SimBodyObstacle(new SimActorId(2), new SimVec2(0, 1000), 280) };
        public void Collect(SimActorId selfId, List<SimBodyObstacle> results)
        { results.Clear(); results.AddRange(Bodies); }
    }
}

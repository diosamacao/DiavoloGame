using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>客机预测卡肉：重叠只 RequestHitStop，不走 Pipeline。</summary>
public sealed class PredictedHitStopTests
{
    /// <summary>攻击盒盖住目标时写入卡肉帧。</summary>
    [Test]
    public void ApplyPredictedHitStop_Overlap_RequestsHitStop()
    {
        ActionDefinition action = CreateHitStopAction(hitStopFrames: 7);
        var receiver = new FakeHitReceiver { InstanceId = 3 };
        var pairs = new HashSet<(int, SimActorId)>();
        var target = new FakeHurtboxTarget(
            new SimActorId(9),
            teamId: 2,
            new HitboxOrientedBox(Vector3.zero, Vector3.one, Quaternion.identity));

        try
        {
            HitDetector.ApplyPredictedHitStopAtFrame(
                action,
                frame: 0,
                attackerTeamId: 1,
                resolveAttackBox: (_, _) =>
                    new HitboxOrientedBox(Vector3.zero, Vector3.one * 2f, Quaternion.identity),
                pairs,
                receiver,
                new IHurtboxTarget[] { target },
                new SimActorId(1),
                actionInstanceId: 3);

            Assert.That(receiver.RequestedFrames, Is.EqualTo(7));
            Assert.That(receiver.CollectCalled, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(action);
        }
    }

    /// <summary>未重叠不写卡肉。</summary>
    [Test]
    public void ApplyPredictedHitStop_NoOverlap_DoesNotRequest()
    {
        ActionDefinition action = CreateHitStopAction(hitStopFrames: 7);
        var receiver = new FakeHitReceiver { InstanceId = 3 };
        var target = new FakeHurtboxTarget(
            new SimActorId(9),
            teamId: 2,
            new HitboxOrientedBox(new Vector3(20f, 0f, 0f), Vector3.one * 0.1f, Quaternion.identity));

        try
        {
            HitDetector.ApplyPredictedHitStopAtFrame(
                action,
                frame: 0,
                attackerTeamId: 1,
                resolveAttackBox: (_, _) =>
                    new HitboxOrientedBox(Vector3.zero, Vector3.one * 0.1f, Quaternion.identity),
                new HashSet<(int, SimActorId)>(),
                receiver,
                new IHurtboxTarget[] { target },
                new SimActorId(1),
                actionInstanceId: 3);

            Assert.That(receiver.RequestedFrames, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(action);
        }
    }

    static ActionDefinition CreateHitStopAction(int hitStopFrames)
    {
        ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
        SerializedObject serialized = new SerializedObject(action);
        SerializedProperty hitboxes = serialized
            .FindProperty("timeline")
            .FindPropertyRelative("hitboxStates");
        hitboxes.arraySize = 1;
        SerializedProperty box = hitboxes.GetArrayElementAtIndex(0);
        box.FindPropertyRelative("startFrame").intValue = 0;
        box.FindPropertyRelative("endFrame").intValue = 10;
        SerializedProperty feedback = box
            .FindPropertyRelative("payload")
            .FindPropertyRelative("feedback");
        feedback.FindPropertyRelative("useHitStop").boolValue = true;
        feedback.FindPropertyRelative("hitStopFrames").intValue = hitStopFrames;
        feedback.FindPropertyRelative("hitStopOncePerAction").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return action;
    }

    sealed class FakeHitReceiver : IActionSimHitReceiver
    {
        public int InstanceId;
        public int RequestedFrames;
        public bool CollectCalled;

        public bool ConfirmHit(int actionInstanceId) => actionInstanceId == InstanceId;

        public bool RequestHitStop(int actionInstanceId, int frames, bool oncePerAction)
        {
            if (actionInstanceId != InstanceId)
                return false;
            RequestedFrames = frames;
            return true;
        }
    }

    sealed class FakeHurtboxTarget : ITargetable
    {
        readonly HitboxOrientedBox _hurtbox;

        public FakeHurtboxTarget(SimActorId id, int teamId, HitboxOrientedBox hurtbox)
        {
            SimulationId = id;
            TeamId = teamId;
            _hurtbox = hurtbox;
        }

        public SimActorId SimulationId { get; }
        public Transform TargetTransform => null;
        public Transform AimTransform => null;
        public bool IsAlive => true;
        public float CurrentHealth => 1f;
        public int TeamId { get; }

        public HitboxOrientedBox GetLogicalHurtbox() => _hurtbox;

        public SimCombatPose GetLogicalCombatPose() => default;

        public void OnHit(in ActionHitContext context)
        {
            // 预测卡肉不得走到 OnHit。
        }
    }
}

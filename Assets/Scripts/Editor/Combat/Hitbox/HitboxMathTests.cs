using NUnit.Framework;
using UnityEngine;

/// <summary>验证逻辑坐标 OBB 构建与相交（Editor 程序集，可见运行时 Combat 类型）。</summary>
public sealed class HitboxMathTests
{
    /// <summary>同位置重叠盒应相交。</summary>
    [Test]
    public void Intersects_OverlappingBoxes_ReturnsTrue()
    {
        var a = new HitboxOrientedBox(Vector3.zero, Vector3.one * 0.5f, Quaternion.identity);
        var b = new HitboxOrientedBox(new Vector3(0.2f, 0f, 0f), Vector3.one * 0.5f, Quaternion.identity);
        Assert.That(HitboxMath.Intersects(a, b), Is.True);
    }

    /// <summary>分离盒不应相交。</summary>
    [Test]
    public void Intersects_SeparatedBoxes_ReturnsFalse()
    {
        var a = new HitboxOrientedBox(Vector3.zero, Vector3.one * 0.25f, Quaternion.identity);
        var b = new HitboxOrientedBox(new Vector3(5f, 0f, 0f), Vector3.one * 0.25f, Quaternion.identity);
        Assert.That(HitboxMath.Intersects(a, b), Is.False);
    }

    /// <summary>MotorSim 前移后逻辑 Hurtbox 中心应跟随毫米坐标。</summary>
    [Test]
    public void BuildFromHurtboxLogical_FollowsMotorPose()
    {
        var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, 280);
        motor.TeleportMeters(2f, 3f);
        motor.SetFacingDegrees(0f);
        SimCombatPose pose = SimCombatPose.FromMotor(motor, heightY: 0f);

        var hurtbox = new HurtboxDefinition();
        HitboxOrientedBox box = HitboxMath.BuildFromHurtboxLogical(in pose, hurtbox);
        Assert.That(box.Center.x, Is.EqualTo(2f).Within(0.001f));
        Assert.That(box.Center.z, Is.EqualTo(3f).Within(0.001f));
    }

    /// <summary>朝向 90° 时局部 +Z 偏移应落到世界 +X。</summary>
    [Test]
    public void SimCombatPose_TransformPoint_RespectsYaw()
    {
        var pose = new SimCombatPose(Vector3.zero, yawDegrees: 90f);
        Vector3 world = pose.TransformPoint(new Vector3(0f, 0f, 1f));
        Assert.That(world.x, Is.EqualTo(1f).Within(0.001f));
        Assert.That(world.z, Is.EqualTo(0f).Within(0.001f));
    }
}

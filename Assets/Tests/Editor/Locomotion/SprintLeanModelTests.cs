using NUnit.Framework;
using UnityEngine;

/// <summary>L-DIR4 SprintLeanModel：目标由偏角决定，实际值经 engage/recover 平滑。</summary>
public sealed class SprintLeanModelTests
{
    static SprintLeanSettings Instant(
        float maxLeanDeg = 8f,
        float deadZoneDeg = 0f,
        float maxEngageYawDeg = 40f) =>
        new(
            maxLeanDeg: maxLeanDeg,
            deadZoneDeg: deadZoneDeg,
            maxEngageYawDeg: maxEngageYawDeg,
            leanEngageSmoothTime: 0f,
            leanRecoverSmoothTime: 0f);

    [Test]
    public void Target_DeadZone_IsZero()
    {
        var settings = Instant(maxLeanDeg: 8f, deadZoneDeg: 10f, maxEngageYawDeg: 45f);
        Assert.That(SprintLeanModel.ComputeTargetLean01(5f, settings), Is.EqualTo(0f));
    }

    [Test]
    public void Target_WishRight_PositiveSignedAngle_NegativeLean()
    {
        // Unity: SignedAngle(forward, right) ≈ +90 → 右倾 lean<0
        var settings = Instant(maxLeanDeg: 8f, deadZoneDeg: 0f, maxEngageYawDeg: 40f);
        Assert.That(SprintLeanModel.ComputeTargetLean01(40f, settings), Is.EqualTo(-1f).Within(0.001f));
    }

    [Test]
    public void Target_WishLeft_NegativeSignedAngle_PositiveLean()
    {
        var settings = Instant(maxLeanDeg: 8f, deadZoneDeg: 0f, maxEngageYawDeg: 40f);
        Assert.That(SprintLeanModel.ComputeTargetLean01(-40f, settings), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void Tick_InstantSmooth_TracksTargetDirectly()
    {
        var model = new SprintLeanModel();
        var settings = Instant(maxLeanDeg: 10f, deadZoneDeg: 0f, maxEngageYawDeg: 90f);

        model.Tick(settings, Vector3.forward, Vector3.right, allowLean: true, deltaTime: 0.016f);
        Assert.That(model.Lean01, Is.EqualTo(-1f).Within(0.001f));
        Assert.That(SprintLeanModel.ToRollDegrees(model.Lean01, settings), Is.EqualTo(-10f).Within(0.001f));

        model.Tick(
            settings,
            Quaternion.Euler(0f, 45f, 0f) * Vector3.forward,
            Vector3.right,
            allowLean: true,
            deltaTime: 0.016f);
        Assert.That(model.Lean01, Is.EqualTo(-0.5f).Within(0.05f));

        model.Tick(settings, Vector3.right, Vector3.right, allowLean: true, deltaTime: 0.016f);
        Assert.That(model.Lean01, Is.EqualTo(0f));
    }

    [Test]
    public void Tick_EngageSmooth_DoesNotReachMaxInOneFrame()
    {
        var model = new SprintLeanModel();
        var settings = new SprintLeanSettings(
            maxLeanDeg: 10f,
            deadZoneDeg: 0f,
            maxEngageYawDeg: 90f,
            leanEngageSmoothTime: 0.25f,
            leanRecoverSmoothTime: 0.25f);

        model.Tick(settings, Vector3.forward, Vector3.right, allowLean: true, deltaTime: 0.016f);
        // SmoothDamp(0.25s, 16ms) 首帧约 0.007，只要求动了且未满倾。
        Assert.That(Mathf.Abs(model.Lean01), Is.GreaterThan(0.001f));
        Assert.That(Mathf.Abs(model.Lean01), Is.LessThan(0.95f));
    }

    [Test]
    public void Tick_RecoverSmooth_DoesNotSnapToZeroInOneFrame()
    {
        var model = new SprintLeanModel();
        var settings = new SprintLeanSettings(
            maxLeanDeg: 10f,
            deadZoneDeg: 0f,
            maxEngageYawDeg: 90f,
            leanEngageSmoothTime: 0f,
            leanRecoverSmoothTime: 0.3f);

        // 先瞬时满倾
        model.Tick(settings, Vector3.forward, Vector3.right, allowLean: true, deltaTime: 0.016f);
        Assert.That(model.Lean01, Is.EqualTo(-1f).Within(0.001f));

        // 对齐后一帧不应立刻到 0
        model.Tick(settings, Vector3.right, Vector3.right, allowLean: true, deltaTime: 0.016f);
        Assert.That(Mathf.Abs(model.Lean01), Is.GreaterThan(0.05f));
        Assert.That(Mathf.Abs(model.Lean01), Is.LessThan(1f));
    }

    [Test]
    public void Tick_AlignedWish_EventuallyExactZero()
    {
        var model = new SprintLeanModel();
        var settings = new SprintLeanSettings(
            maxLeanDeg: 14f,
            deadZoneDeg: 3f,
            maxEngageYawDeg: 70f,
            leanEngageSmoothTime: 0.05f,
            leanRecoverSmoothTime: 0.05f);

        model.Tick(settings, Vector3.forward, Vector3.right, allowLean: true, deltaTime: 0.5f);
        Assert.That(Mathf.Abs(model.Lean01), Is.GreaterThan(0.2f));

        for (int i = 0; i < 40; i++)
            model.Tick(settings, Vector3.right, Vector3.right, allowLean: true, deltaTime: 0.05f);

        Assert.That(model.Lean01, Is.EqualTo(0f));
    }

    [Test]
    public void MaxLeanZero_Disables()
    {
        var model = new SprintLeanModel();
        var settings = new SprintLeanSettings(maxLeanDeg: 0f);
        model.Tick(settings, Vector3.forward, Vector3.right, allowLean: true, deltaTime: 1f);
        Assert.That(model.Lean01, Is.EqualTo(0f));
    }
}

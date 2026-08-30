using NUnit.Framework;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

/// <summary>官方 Spline 相机求值、恒速、缓存与世界 Pose 测试。</summary>
public sealed class CameraSplineEvaluatorTests
{
    /// <summary>线性路径按标准化时间返回精确局部位置。</summary>
    [Test]
    public void TryEvaluate_LinearSpline_ReturnsExpectedPosition()
    {
        var shot = new CameraShotNotifyState();
        SetTwoKnotSpline(shot, new float3(0f, 0f, 0f), new float3(10f, 0f, 0f));
        var spline = shot.PositionSpline;

        bool valid = CameraSplineEvaluator.TryEvaluate(
            spline,
            AnimationCurve.Linear(0f, 0f, 1f, 1f),
            false,
            0.5f,
            out Vector3 position,
            out Vector3 tangent);

        Assert.That(valid, Is.True);
        Assert.That(position.x, Is.EqualTo(5f).Within(0.001f));
        Assert.That(Vector3.Dot(tangent, Vector3.right), Is.GreaterThan(0.999f));
    }

    /// <summary>恒速模式在不均匀 Bezier 上把等进度映射为近似等弧长。</summary>
    [Test]
    public void TryEvaluate_ConstantSpeed_UsesArcLength()
    {
        var shot = new CameraShotNotifyState();
        SetTwoKnotSpline(shot, new float3(0f, 0f, 0f), new float3(10f, 0f, 2f));
        var spline = shot.PositionSpline;

        CameraSplineEvaluator.TryEvaluate(spline, null, true, 0f, out Vector3 p0, out Vector3 _);
        CameraSplineEvaluator.TryEvaluate(spline, null, true, 0.5f, out Vector3 p1, out Vector3 _);
        CameraSplineEvaluator.TryEvaluate(spline, null, true, 1f, out Vector3 p2, out Vector3 _);

        Assert.That(Vector3.Distance(p0, p1), Is.EqualTo(Vector3.Distance(p1, p2)).Within(0.03f));
    }

    /// <summary>高曲率路径的中途帧不能因首尾直线距离较短而提前钳制到终点。</summary>
    [Test]
    public void TryEvaluate_ConstantSpeed_HighCurvatureDoesNotReachEndEarly()
    {
        var shot = new CameraShotNotifyState();
        SetTwoKnotSpline(shot, new float3(0f, 0f, 0f), new float3(1f, 0f, 0f));
        CameraSplineCurveRuleUtility.Apply(shot.PositionSpline, CameraSplineCurveRule.ArcUp);
        var start = shot.PositionSpline[0];
        start.TangentOut *= 8f;
        shot.PositionSpline[0] = start;
        var end = shot.PositionSpline[1];
        end.TangentIn *= 8f;
        shot.PositionSpline[1] = end;

        CameraSplineEvaluator.TryEvaluate(
            shot.PositionSpline,
            null,
            true,
            0.7f,
            out Vector3 beforeEnd,
            out Vector3 _);
        CameraSplineEvaluator.TryEvaluate(
            shot.PositionSpline,
            null,
            true,
            1f,
            out Vector3 atEnd,
            out Vector3 _);

        Assert.That(Vector3.Distance(beforeEnd, atEnd), Is.GreaterThan(0.05f));
        Assert.That(atEnd.x, Is.EqualTo(1f).Within(0.001f));
    }

    /// <summary>恒速求值会累计多个 Knot 之间的曲线段，并在后半程进入后续段。</summary>
    [Test]
    public void TryEvaluate_ConstantSpeed_TraversesMultipleKnots()
    {
        var shot = new CameraShotNotifyState();
        SetTwoKnotSpline(shot, new float3(0f, 0f, 0f), new float3(20f, 0f, 0f));
        shot.PositionSpline.Insert(1, new float3(10f, 0f, 0f));

        CameraSplineEvaluator.TryEvaluate(
            shot.PositionSpline,
            null,
            true,
            0.75f,
            out Vector3 position,
            out Vector3 _);

        Assert.That(position.x, Is.EqualTo(15f).Within(0.03f));
    }

    /// <summary>修改 Knot 后官方 Spline 会失效并重建内部长度缓存。</summary>
    [Test]
    public void SplineLengthCache_KnotChange_InvalidatesLength()
    {
        var shot = new CameraShotNotifyState();
        SetTwoKnotSpline(shot, new float3(0f, 0f, 0f), new float3(1f, 0f, 0f));
        var spline = shot.PositionSpline;
        float before = spline.GetLength();
        var end = spline[1];
        end.Position = new float3(3f, 0f, 0f);

        spline[1] = end;

        Assert.That(before, Is.EqualTo(1f).Within(0.001f));
        Assert.That(spline.GetLength(), Is.EqualTo(3f).Within(0.001f));
    }

    /// <summary>上拱预设保留端点，并自动生成高于端点连线的中段。</summary>
    [Test]
    public void CurveRule_ArcUp_BuildsEndpointDrivenArc()
    {
        var shot = new CameraShotNotifyState();
        SetTwoKnotSpline(shot, new float3(0f, 0f, 0f), new float3(4f, 0f, 0f));

        bool applied = CameraSplineCurveRuleUtility.Apply(
            shot.PositionSpline,
            CameraSplineCurveRule.ArcUp);
        CameraSplineEvaluator.TryEvaluate(
            shot.PositionSpline,
            null,
            false,
            0.5f,
            out Vector3 midpoint,
            out Vector3 _);

        Assert.That(applied, Is.True);
        Assert.That(shot.PositionSpline.Count, Is.EqualTo(2));
        Assert.That(midpoint.x, Is.EqualTo(2f).Within(0.001f));
        Assert.That(midpoint.y, Is.EqualTo(1f).Within(0.001f));
    }

    /// <summary>Custom 规则不得清理中间 Knot 或覆盖作者切线。</summary>
    [Test]
    public void CurveRule_Custom_PreservesAuthoredSpline()
    {
        var shot = new CameraShotNotifyState();
        shot.PositionSpline.Insert(1, new float3(1f, 2f, 0f));

        bool applied = CameraSplineCurveRuleUtility.Apply(
            shot.PositionSpline,
            CameraSplineCurveRule.Custom);

        Assert.That(applied, Is.False);
        Assert.That(shot.PositionSpline.Count, Is.EqualTo(3));
        Assert.That(shot.PositionSpline[1].Position.y, Is.EqualTo(2f).Within(0.001f));
    }

    /// <summary>少于两个 Knot 的路径必须显式判无效。</summary>
    [Test]
    public void TryEvaluate_OneKnot_ReturnsFalse()
    {
        var shot = new CameraShotNotifyState();
        var spline = shot.PositionSpline;
        spline.RemoveAt(1);

        Assert.That(CameraSplineEvaluator.TryEvaluate(
            spline,
            null,
            true,
            0.5f,
            out _,
            out _), Is.False);
    }

    /// <summary>PoseResolver 将位置与 LookAt 的独立参考系正确变换到世界。</summary>
    [Test]
    public void TryResolvePose_TransformsSplineAndLookAtBindings()
    {
        var shot = new CameraShotNotifyState();
        SetTimelineFrame(shot, "startFrame", 0);
        SetTimelineFrame(shot, "endFrame", 10);
        shot.PositionSpline.Clear();
        shot.PositionSpline.Add(new float3(0f, 0f, 0f));
        shot.PositionSpline.Add(new float3(0f, 0f, 2f));
        var reference = new CameraReferencePose(
            new Vector3(1f, 0f, 0f),
            Quaternion.Euler(0f, 90f, 0f));
        var lookAt = new CameraReferencePose(new Vector3(5f, 0f, 0f), Quaternion.identity);

        bool valid = CameraShotPoseResolver.TryResolvePose(shot, reference, lookAt, 10, out CameraShotPose pose);

        Assert.That(valid, Is.True);
        Assert.That(pose.WorldPosition.x, Is.EqualTo(3f).Within(0.001f));
        Assert.That(pose.WorldPosition.z, Is.EqualTo(0f).Within(0.001f));
        Assert.That(pose.WorldLookAt, Is.EqualTo(new Vector3(5f, 1.2f, 0f)));
        Assert.That(pose.FieldOfView, Is.EqualTo(60f).Within(0.001f));
    }

    /// <summary>从 Runtime 暴露的默认 Spline 实例构造两点路径，避免测试程序集重复导入包源码类型。</summary>
    static void SetTwoKnotSpline(CameraShotNotifyState shot, float3 start, float3 end)
    {
        var spline = shot.PositionSpline;
        var startKnot = spline[0];
        startKnot.Position = start;
        spline[0] = startKnot;
        var endKnot = spline[1];
        endKnot.Position = end;
        spline[1] = endKnot;
    }

    /// <summary>测试中写入 ActionTimelineItem 的私有逻辑帧字段。</summary>
    static void SetTimelineFrame(CameraShotNotifyState shot, string fieldName, int value)
    {
        FieldInfo field = typeof(ActionTimelineItem).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(shot, value);
    }
}

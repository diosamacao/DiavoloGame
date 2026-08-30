using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Camera Timeline 窗查询与模拟隔离测试。</summary>
public sealed class CameraShotNotifyFrameTests
{
    /// <summary>重叠窗口返回最高优先级 Shot。</summary>
    [Test]
    public void GetActiveCameraShotAtFrame_ReturnsHighestPriority()
    {
        var low = BuildShot("low", 2, 8, 1);
        var high = BuildShot("high", 4, 6, 10);
        var timeline = new ActionTimeline();
        SetField(timeline, "cameraShotStates", new[] { low, high });

        Assert.That(timeline.GetActiveCameraShotAtFrame(3), Is.SameAs(low));
        Assert.That(timeline.GetActiveCameraShotAtFrame(5), Is.SameAs(high));
        Assert.That(timeline.GetActiveCameraShotAtFrame(9), Is.Null);
    }

    /// <summary>Camera 窗不得进入 ActionTimelineRunner 使用的通用 State 枚举。</summary>
    [Test]
    public void EnumerateStates_DoesNotIncludeCameraShots()
    {
        var shot = BuildShot("shot", 0, 5, 0);
        var timeline = new ActionTimeline();
        SetField(timeline, "cameraShotStates", new[] { shot });

        Assert.That(timeline.EnumerateStates().Contains(shot), Is.False);
        Assert.That(timeline.CameraShotStates, Has.Length.EqualTo(1));
    }

    /// <summary>窗口逻辑帧线性映射到 0～1 Spline 时间。</summary>
    [Test]
    public void EvaluateNormalizedTime_UsesWindowFrames()
    {
        CameraShotNotifyState shot = BuildShot("spline", 10, 20, 0);

        Assert.That(shot.EvaluateNormalizedTime(10), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(shot.EvaluateNormalizedTime(15), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(shot.EvaluateNormalizedTime(20), Is.EqualTo(1f).Within(0.0001f));
    }

    /// <summary>Scene 构图写回可更新 LookAt，并在同一窗口进度覆盖而非重复添加 FOV Key。</summary>
    [Test]
    public void SceneCompositionAuthoring_UpdatesLookAtAndReusesFovKey()
    {
        CameraShotNotifyState shot = BuildShot("capture", 0, 10, 0);
        var lookAt = new Vector3(1f, 2f, 3f);
        shot.SetLookAtLocalPosition(lookAt);
        int initialKeyCount = shot.FieldOfViewCurve.length;

        shot.SetFieldOfViewKey(0.5f, 70f);
        int capturedKeyCount = shot.FieldOfViewCurve.length;
        shot.SetFieldOfViewKey(0.5f, 80f);

        Assert.That(shot.LookAtLocalPosition, Is.EqualTo(lookAt));
        Assert.That(capturedKeyCount, Is.EqualTo(initialKeyCount + 1));
        Assert.That(shot.FieldOfViewCurve.length, Is.EqualTo(capturedKeyCount));
        Assert.That(
            CameraShotPoseResolver.ResolveFieldOfView(shot, 0.5f),
            Is.EqualTo(80f).Within(0.001f));
    }

    /// <summary>构造测试窗口；反射只用于写入 Unity 私有序列化字段。</summary>
    static CameraShotNotifyState BuildShot(
        string id,
        int startFrame,
        int endFrame,
        int priority)
    {
        var shot = new CameraShotNotifyState();
        shot.SetId(id);
        SetField(typeof(ActionTimelineItem), shot, "startFrame", startFrame);
        SetField(typeof(ActionTimelineItem), shot, "endFrame", endFrame);
        SetField(typeof(ActionTimelineItem), shot, "priority", priority);
        return shot;
    }

    /// <summary>写入对象自身声明的私有字段。</summary>
    static void SetField(object target, string name, object value) =>
        SetField(target.GetType(), target, name, value);

    /// <summary>写入指定声明类型的私有字段。</summary>
    static void SetField(Type declaringType, object target, string name, object value)
    {
        FieldInfo field = declaringType.GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field: {declaringType.Name}.{name}");
        field.SetValue(target, value);
    }
}

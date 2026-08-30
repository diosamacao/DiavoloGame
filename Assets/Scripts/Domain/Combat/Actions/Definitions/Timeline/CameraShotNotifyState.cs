using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>Action Timeline 上的一段表现镜头窗口；模拟核不枚举或执行该窗口。</summary>
[Serializable]
public sealed class CameraShotNotifyState : ActionNotifyState
{
    [SerializeField] bool overrideCameraPose = true;
    [SerializeField] CameraTransformBinding referenceBinding = new();
    [SerializeField] Spline positionSpline = CreateDefaultSpline();
    [SerializeField] CameraSplineCurveRule splineCurveRule = CameraSplineCurveRule.Custom;
    [SerializeField] AnimationCurve speedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] bool constantSpeed = true;
    [SerializeField] CameraTransformBinding lookAtBinding = new();
    [SerializeField] Vector3 lookAtLocalPosition = new(0f, 1.2f, 0f);
    [SerializeField] AnimationCurve fieldOfViewCurve = AnimationCurve.Linear(0f, 60f, 1f, 60f);
    [SerializeField, Min(0f)] float blendInSeconds = 0.08f;
    [SerializeField] bool inheritPosition = true;
    [SerializeField] bool holdFollow;
    [SerializeField] CameraShakeProfile impulseOnEnter;

    /// <summary>是否由样条覆盖机位；关闭时该窗只应用 Hold/Impulse 等反馈。</summary>
    public bool OverrideCameraPose => overrideCameraPose;

    /// <summary>位置样条的模型无关参考系。</summary>
    public CameraTransformBinding ReferenceBinding => referenceBinding;

    /// <summary>官方 Unity Spline；开启机位覆盖时它是位置唯一真源。</summary>
    public Spline PositionSpline => positionSpline;

    /// <summary>预设规则只由首尾端点生成路径；Custom 保留完整 Knot/Tangent 作者数据。</summary>
    public CameraSplineCurveRule SplineCurveRule => splineCurveRule;

    /// <summary>动作窗时间到样条进度的映射。</summary>
    public AnimationCurve SpeedCurve => speedCurve;

    /// <summary>是否按官方 Spline 长度重新参数化，实现近似恒速。</summary>
    public bool ConstantSpeed => constantSpeed;

    /// <summary>观察点的模型无关参考系。</summary>
    public CameraTransformBinding LookAtBinding => lookAtBinding;

    /// <summary>相对 LookAt Binding 的局部观察点。</summary>
    public Vector3 LookAtLocalPosition => lookAtLocalPosition;

    /// <summary>窗口进度到 FOV 的曲线，曲线值单位为度。</summary>
    public AnimationCurve FieldOfViewCurve => fieldOfViewCurve;

    /// <summary>进入该段时的 Cinemachine Blend 时长。</summary>
    public float BlendInSeconds => Mathf.Max(0f, blendInSeconds);

    /// <summary>抢权时是否继承当前实际相机姿态。</summary>
    public bool InheritPosition => inheritPosition;

    /// <summary>窗口内是否钉住 Gameplay CameraRig 的进入帧跟随点。</summary>
    public bool HoldFollow => holdFollow;

    /// <summary>首次进入窗口时播放的可选 Impulse。</summary>
    public CameraShakeProfile ImpulseOnEnter => impulseOnEnter;

    /// <summary>写入 LookAt Binding 局部观察点，供 Action Editor Scene 构图捕获使用。</summary>
    public void SetLookAtLocalPosition(Vector3 value) => lookAtLocalPosition = value;

    /// <summary>在指定窗口进度写入 FOV Key；已有同帧 Key 时只替换其值。</summary>
    public void SetFieldOfViewKey(float normalizedTime, float fieldOfView)
    {
        float time = Mathf.Clamp01(normalizedTime);
        float value = Mathf.Clamp(fieldOfView, 1f, 179f);
        fieldOfViewCurve ??= new AnimationCurve();
        for (int i = 0; i < fieldOfViewCurve.length; i++)
        {
            Keyframe key = fieldOfViewCurve[i];
            if (Mathf.Abs(key.time - time) > 0.0001f)
                continue;

            key.value = value;
            fieldOfViewCurve.MoveKey(i, key);
            return;
        }

        fieldOfViewCurve.AddKey(new Keyframe(time, value));
    }

    /// <summary>将闭区间动作帧归一化到 0～1。</summary>
    public float EvaluateNormalizedTime(int frame)
    {
        int duration = EndFrame - StartFrame;
        if (duration <= 0)
            return 0f;

        return Mathf.Clamp01((frame - StartFrame) / (float)duration);
    }

    /// <summary>Action Editor 新建窗口时重置独立 Spline/曲线，避免 Unity 数组扩容复制上一段数据。</summary>
    public void ResetSplineDefaults()
    {
        overrideCameraPose = true;
        referenceBinding = new CameraTransformBinding();
        positionSpline = CreateDefaultSpline();
        splineCurveRule = CameraSplineCurveRule.Linear;
        CameraSplineCurveRuleUtility.Apply(positionSpline, splineCurveRule);
        speedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        constantSpeed = true;
        lookAtBinding = new CameraTransformBinding();
        lookAtLocalPosition = new Vector3(0f, 1.2f, 0f);
        fieldOfViewCurve = AnimationCurve.Linear(0f, 60f, 1f, 60f);
    }

    /// <summary>为新 Camera 窗创建可直接分辨和拖拽首尾端点的默认路径。</summary>
    static Spline CreateDefaultSpline()
    {
        var start = new float3(0f, 1.4f, -4f);
        var end = new float3(0f, 1.4f, -3f);
        return new Spline(
            new[] { new BezierKnot(start), new BezierKnot(end) },
            closed: false);
    }
}

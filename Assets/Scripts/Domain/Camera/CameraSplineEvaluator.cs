using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>官方 Unity Spline 的相机帧求值薄封装；统一速度曲线与恒速重映射。</summary>
public static class CameraSplineEvaluator
{
    /// <summary>有效相机位置路径至少需要两个 Knot。</summary>
    public static bool IsValid(Spline spline) => spline != null && spline.Count >= 2;

    /// <summary>按 0～1 窗口进度求局部位置与切线；无效路径返回 false，禁止旧 Offset 回退。</summary>
    public static bool TryEvaluate(
        Spline spline,
        AnimationCurve speedCurve,
        bool constantSpeed,
        float normalizedTime,
        out Vector3 localPosition,
        out Vector3 localTangent)
    {
        localPosition = default;
        localTangent = Vector3.forward;
        if (!IsValid(spline))
            return false;

        float progress = Mathf.Clamp01(normalizedTime);
        if (speedCurve != null && speedCurve.length > 0)
            progress = Mathf.Clamp01(speedCurve.Evaluate(progress));

        float3 position;
        float3 tangent;
        if (constantSpeed)
        {
            EvaluateAtArcLength(spline, progress, out position, out tangent);
        }
        else
        {
            position = spline.EvaluatePosition(progress);
            tangent = spline.EvaluateTangent(progress);
        }

        localPosition = new Vector3(position.x, position.y, position.z);
        Vector3 resolvedTangent = new(tangent.x, tangent.y, tangent.z);
        if (resolvedTangent.sqrMagnitude > 0.000001f)
            localTangent = resolvedTangent.normalized;
        return IsFinite(localPosition);
    }

    /// <summary>沿各 Bezier 段累计弧长定位，避免 LinearDistance 把首尾直线距离误当路径距离。</summary>
    static void EvaluateAtArcLength(
        Spline spline,
        float progress,
        out float3 position,
        out float3 tangent)
    {
        if (progress <= 0f)
        {
            position = spline.EvaluatePosition(0f);
            tangent = spline.EvaluateTangent(0f);
            return;
        }

        if (progress >= 1f)
        {
            position = spline.EvaluatePosition(1f);
            tangent = spline.EvaluateTangent(1f);
            return;
        }

        int curveCount = spline.Closed ? spline.Count : spline.Count - 1;
        float remainingDistance = spline.GetLength() * progress;
        for (int curveIndex = 0; curveIndex < curveCount; curveIndex++)
        {
            float curveLength = spline.GetCurveLength(curveIndex);
            bool isLastCurve = curveIndex == curveCount - 1;
            if (remainingDistance > curveLength && !isLastCurve)
            {
                remainingDistance -= curveLength;
                continue;
            }

            // Spline 自带每段 Distance→Interpolation LUT，能按真实曲线累计长度定位段内 t。
            float curveT = curveLength > 0.000001f
                ? spline.GetCurveInterpolation(
                    curveIndex,
                    Mathf.Clamp(remainingDistance, 0f, curveLength))
                : 0f;
            BezierCurve curve = spline.GetCurve(curveIndex);
            position = CurveUtility.EvaluatePosition(curve, curveT);
            tangent = CurveUtility.EvaluateTangent(curve, curveT);
            return;
        }

        position = spline.EvaluatePosition(1f);
        tangent = spline.EvaluateTangent(1f);
    }

    /// <summary>检查向量是否可安全写入 Transform/Cinemachine。</summary>
    public static bool IsFinite(Vector3 value) =>
        IsFinite(value.x)
        && IsFinite(value.y)
        && IsFinite(value.z);

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

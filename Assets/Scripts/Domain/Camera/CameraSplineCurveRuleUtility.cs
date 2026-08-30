using Unity.Mathematics;
using UnityEngine.Splines;

/// <summary>把两端点和预设几何规则编译为运行时直接求值的官方 Spline。</summary>
public static class CameraSplineCurveRuleUtility
{
    const float ArcMidpointOffsetFactor = 0.25f;

    /// <summary>保留首尾位置并重建两点路径；Custom 不修改作者数据。</summary>
    public static bool Apply(Spline spline, CameraSplineCurveRule rule)
    {
        if (spline == null || spline.Count < 2 || rule == CameraSplineCurveRule.Custom)
            return false;

        float3 start = spline[0].Position;
        float3 end = spline[spline.Count - 1].Position;
        spline.Closed = false;
        spline.Clear();
        spline.Add(new BezierKnot(start), TangentMode.Broken);
        spline.Add(new BezierKnot(end), TangentMode.Broken);

        if (rule == CameraSplineCurveRule.Linear)
        {
            spline.SetTangentMode(TangentMode.Linear);
            return true;
        }

        float3 arcAxis = ResolveArcAxis(rule);
        float distance = math.distance(start, end);
        float3 midpoint = (start + end) * 0.5f;
        // 二次 Bezier 控制点转三次 Bezier，可让曲线中点稳定偏移端点距离的四分之一。
        float3 quadraticControl = midpoint + arcAxis * distance * (ArcMidpointOffsetFactor * 2f);
        float3 firstControl = start + (quadraticControl - start) * (2f / 3f);
        float3 secondControl = end + (quadraticControl - end) * (2f / 3f);

        var startKnot = new BezierKnot(start)
        {
            TangentOut = firstControl - start,
        };
        var endKnot = new BezierKnot(end)
        {
            TangentIn = secondControl - end,
        };
        spline[0] = startKnot;
        spline[1] = endKnot;
        return true;
    }

    /// <summary>返回 Binding 局部空间中的预设弯曲方向。</summary>
    static float3 ResolveArcAxis(CameraSplineCurveRule rule)
    {
        return rule switch
        {
            CameraSplineCurveRule.ArcUp => new float3(0f, 1f, 0f),
            CameraSplineCurveRule.ArcDown => new float3(0f, -1f, 0f),
            CameraSplineCurveRule.ArcLeft => new float3(-1f, 0f, 0f),
            CameraSplineCurveRule.ArcRight => new float3(1f, 0f, 0f),
            _ => float3.zero,
        };
    }
}

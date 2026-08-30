using UnityEngine;

/// <summary>Editor 与 Runtime 共用的 Binding 与 Spline 世界机位求值器。</summary>
public static class CameraShotPoseResolver
{
    /// <summary>解析模型无关 Binding；自定义 Id 缺失时失败，不回退到旧命名节点。</summary>
    public static bool TryResolveReferencePose(
        CameraTransformBinding binding,
        Transform characterRoot,
        Transform selectedTarget,
        ICameraAnchorProvider characterProvider,
        ICameraAnchorProvider targetProvider,
        out CameraReferencePose pose)
    {
        pose = CameraReferencePose.Identity;
        if (binding == null)
            return false;
        if (binding.Source == CameraBindingSource.World)
            return true;

        Transform root = binding.Source switch
        {
            CameraBindingSource.Character => characterRoot,
            CameraBindingSource.SelectedTarget => selectedTarget,
            _ => null,
        };
        if (root == null)
            return false;

        Transform anchor = root;
        if (!string.IsNullOrWhiteSpace(binding.AnchorId))
        {
            ICameraAnchorProvider provider =
                binding.Source == CameraBindingSource.Character ? characterProvider : targetProvider;
            if (provider == null || !provider.TryResolveCameraAnchor(binding.AnchorId, out anchor) || anchor == null)
                return false;
        }

        pose = new CameraReferencePose(anchor.position, anchor.rotation);
        return true;
    }

    /// <summary>用同一官方 Spline 路径生成运行时与 Editor 共用的世界机位。</summary>
    public static bool TryResolvePose(
        CameraShotNotifyState shot,
        CameraReferencePose referencePose,
        CameraReferencePose lookAtPose,
        int frame,
        out CameraShotPose pose)
    {
        pose = default;
        if (shot == null || !shot.OverrideCameraPose)
            return false;

        float normalizedTime = shot.EvaluateNormalizedTime(frame);
        if (!CameraSplineEvaluator.TryEvaluate(
                shot.PositionSpline,
                shot.SpeedCurve,
                shot.ConstantSpeed,
                normalizedTime,
                out Vector3 localPosition,
                out _))
        {
            return false;
        }

        Vector3 worldPosition = referencePose.TransformPoint(localPosition);
        Vector3 worldLookAt = lookAtPose.TransformPoint(shot.LookAtLocalPosition);
        float fieldOfView = ResolveFieldOfView(shot, normalizedTime);
        pose = new CameraShotPose(worldPosition, worldLookAt, fieldOfView);
        return CameraSplineEvaluator.IsFinite(worldPosition)
            && CameraSplineEvaluator.IsFinite(worldLookAt);
    }

    /// <summary>由 Shot 的 FOV 曲线求值并限制到 Unity Camera 合法范围。</summary>
    public static float ResolveFieldOfView(CameraShotNotifyState shot, float normalizedTime)
    {
        if (shot == null)
            return 60f;
        AnimationCurve curve = shot.FieldOfViewCurve;
        float value = curve != null && curve.length > 0 ? curve.Evaluate(Mathf.Clamp01(normalizedTime)) : 60f;
        return Mathf.Clamp(value, 1f, 179f);
    }
}

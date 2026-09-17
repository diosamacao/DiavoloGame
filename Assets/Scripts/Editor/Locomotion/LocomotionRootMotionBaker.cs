using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>从 AnimationClip 烘焙 Locomotion 根位移轨（Humanoid RootT/Q 或 Generic LocalPosition/Rotation）。</summary>
public static class LocomotionRootMotionBaker
{
    const float DefaultSamplesPerSecond = 60f;

    /// <summary>烘焙单条 Clip；失败返回 Empty。</summary>
    public static LocomotionRootMotionTrack Bake(AnimationClip clip, float samplesPerSecond = DefaultSamplesPerSecond)
    {
        if (clip == null || clip.length <= 0f)
            return LocomotionRootMotionTrack.Empty;

        if (!TryResolveCurves(
                clip,
                out AnimationCurve posX,
                out AnimationCurve posY,
                out AnimationCurve posZ,
                out AnimationCurve rotX,
                out AnimationCurve rotY,
                out AnimationCurve rotZ,
                out AnimationCurve rotW,
                out bool rotationIsEulerY))
        {
            Debug.LogWarning($"LocomotionRootMotionBaker: Clip「{clip.name}」未找到 RootT/LocalPosition 曲线，跳过烘焙。", clip);
            return LocomotionRootMotionTrack.Empty;
        }

        float duration = clip.length;
        int frameCount = Mathf.Max(1, Mathf.CeilToInt(duration * Mathf.Max(1f, samplesPerSecond)));
        int count = frameCount + 1;
        var positions = new Vector3[count];
        var yaws = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = duration * i / (count - 1);
            float x = Evaluate(posX, t);
            float y = Evaluate(posY, t);
            float z = Evaluate(posZ, t);
            positions[i] = new Vector3(x, y, z);

            if (rotationIsEulerY)
            {
                yaws[i] = Evaluate(rotY, t);
            }
            else
            {
                float qx = Evaluate(rotX, t);
                float qy = Evaluate(rotY, t);
                float qz = Evaluate(rotZ, t);
                float qw = rotW != null ? Evaluate(rotW, t) : 1f;
                float magSq = qx * qx + qy * qy + qz * qz + qw * qw;
                Quaternion q = magSq > 0.0001f
                    ? new Quaternion(qx, qy, qz, qw).normalized
                    : Quaternion.identity;
                yaws[i] = q.eulerAngles.y;
            }
        }

        // 相对起点归零，避免绝对坐标把角色甩飞
        Vector3 origin = positions[0];
        float yaw0 = yaws[0];
        for (int i = 0; i < count; i++)
        {
            positions[i] -= origin;
            yaws[i] = Mathf.DeltaAngle(yaw0, yaws[i]);
        }

        return LocomotionRootMotionTrack.Create(frameCount, positions, yaws);
    }

    static float Evaluate(AnimationCurve curve, float time) =>
        curve != null ? curve.Evaluate(time) : 0f;

    static bool TryResolveCurves(
        AnimationClip clip,
        out AnimationCurve posX,
        out AnimationCurve posY,
        out AnimationCurve posZ,
        out AnimationCurve rotX,
        out AnimationCurve rotY,
        out AnimationCurve rotZ,
        out AnimationCurve rotW,
        out bool rotationIsEulerY)
    {
        posX = posY = posZ = rotX = rotY = rotZ = rotW = null;
        rotationIsEulerY = false;

        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        AnimationCurve humanX = null, humanY = null, humanZ = null;
        AnimationCurve humanQx = null, humanQy = null, humanQz = null, humanQw = null;
        var localCandidates = new Dictionary<string, LocalCurveSet>(StringComparer.Ordinal);

        foreach (EditorCurveBinding binding in bindings)
        {
            string prop = binding.propertyName;
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null)
                continue;

            if (IsLocalTransformProperty(prop))
            {
                string path = binding.path ?? string.Empty;
                if (!localCandidates.TryGetValue(path, out LocalCurveSet candidate))
                {
                    candidate = new LocalCurveSet(path);
                    localCandidates.Add(path, candidate);
                }
                candidate.Assign(prop, curve);
                continue;
            }

            switch (prop)
            {
                case "RootT.x": humanX = curve; break;
                case "RootT.y": humanY = curve; break;
                case "RootT.z": humanZ = curve; break;
                case "RootQ.x": humanQx = curve; break;
                case "RootQ.y": humanQy = curve; break;
                case "RootQ.z": humanQz = curve; break;
                case "RootQ.w": humanQw = curve; break;
            }
        }

        if (humanX != null || humanZ != null)
        {
            posX = humanX;
            posY = humanY;
            posZ = humanZ;
            rotX = humanQx;
            rotY = humanQy;
            rotZ = humanQz;
            rotW = humanQw;
            rotationIsEulerY = false;
            return true;
        }

        LocalCurveSet local = SelectRootCandidate(localCandidates);
        if (local != null)
        {
            posX = local.PositionX;
            posY = local.PositionY;
            posZ = local.PositionZ;
            if (local.EulerY != null)
            {
                rotY = local.EulerY;
                rotX = local.EulerX;
                rotZ = local.EulerZ;
                rotationIsEulerY = true;
            }
            else
            {
                rotX = local.RotationX;
                rotY = local.RotationY;
                rotZ = local.RotationZ;
                rotW = local.RotationW;
                rotationIsEulerY = false;
            }

            return true;
        }

        return false;
    }

    /// <summary>识别 Generic Transform 曲线；必须按 binding.path 分组，禁止把不同骨骼通道拼成伪根轨。</summary>
    static bool IsLocalTransformProperty(string propertyName) =>
        propertyName == "m_LocalPosition.x"
        || propertyName == "m_LocalPosition.y"
        || propertyName == "m_LocalPosition.z"
        || propertyName == "localEulerAnglesRaw.x"
        || propertyName == "localEulerAnglesRaw.y"
        || propertyName == "localEulerAnglesRaw.z"
        || propertyName == "localEulerAnglesBaked.x"
        || propertyName == "localEulerAnglesBaked.y"
        || propertyName == "localEulerAnglesBaked.z"
        || propertyName == "m_LocalEulerAngles.x"
        || propertyName == "m_LocalEulerAngles.y"
        || propertyName == "m_LocalEulerAngles.z"
        || propertyName == "m_LocalRotation.x"
        || propertyName == "m_LocalRotation.y"
        || propertyName == "m_LocalRotation.z"
        || propertyName == "m_LocalRotation.w";

    /// <summary>优先空路径或名含 Root/Hips/Pelvis 的最浅 Transform，避免误取末尾手脚骨曲线。</summary>
    static LocalCurveSet SelectRootCandidate(Dictionary<string, LocalCurveSet> candidates)
    {
        LocalCurveSet best = null;
        foreach (LocalCurveSet candidate in candidates.Values)
        {
            if (!candidate.HasPlanarPosition)
                continue;
            if (best == null || candidate.CompareRootPriority(best) < 0)
                best = candidate;
        }
        return best;
    }

    /// <summary>保存同一 Transform path 的 Generic 位移与旋转通道。</summary>
    sealed class LocalCurveSet
    {
        public LocalCurveSet(string path) => Path = path ?? string.Empty;

        public string Path { get; }
        public AnimationCurve PositionX { get; private set; }
        public AnimationCurve PositionY { get; private set; }
        public AnimationCurve PositionZ { get; private set; }
        public AnimationCurve EulerX { get; private set; }
        public AnimationCurve EulerY { get; private set; }
        public AnimationCurve EulerZ { get; private set; }
        public AnimationCurve RotationX { get; private set; }
        public AnimationCurve RotationY { get; private set; }
        public AnimationCurve RotationZ { get; private set; }
        public AnimationCurve RotationW { get; private set; }
        public bool HasPlanarPosition => PositionX != null || PositionZ != null;

        /// <summary>把单个绑定写入该 path；调用方已保证属性属于 Generic Transform。</summary>
        public void Assign(string propertyName, AnimationCurve curve)
        {
            switch (propertyName)
            {
                case "m_LocalPosition.x": PositionX = curve; break;
                case "m_LocalPosition.y": PositionY = curve; break;
                case "m_LocalPosition.z": PositionZ = curve; break;
                case "localEulerAnglesRaw.x":
                case "localEulerAnglesBaked.x":
                case "m_LocalEulerAngles.x": EulerX = curve; break;
                case "localEulerAnglesRaw.y":
                case "localEulerAnglesBaked.y":
                case "m_LocalEulerAngles.y": EulerY = curve; break;
                case "localEulerAnglesRaw.z":
                case "localEulerAnglesBaked.z":
                case "m_LocalEulerAngles.z": EulerZ = curve; break;
                case "m_LocalRotation.x": RotationX = curve; break;
                case "m_LocalRotation.y": RotationY = curve; break;
                case "m_LocalRotation.z": RotationZ = curve; break;
                case "m_LocalRotation.w": RotationW = curve; break;
            }
        }

        /// <summary>按根命名优先级、层级深度与稳定路径排序。</summary>
        public int CompareRootPriority(LocalCurveSet other)
        {
            int rank = RootNameRank(Path).CompareTo(RootNameRank(other.Path));
            if (rank != 0)
                return rank;
            int depth = PathDepth(Path).CompareTo(PathDepth(other.Path));
            return depth != 0
                ? depth
                : string.CompareOrdinal(Path, other.Path);
        }

        static int RootNameRank(string path)
        {
            if (string.IsNullOrEmpty(path))
                return 0;
            string leaf = path;
            int slash = leaf.LastIndexOf('/');
            if (slash >= 0)
                leaf = leaf.Substring(slash + 1);
            if (leaf.IndexOf("root", StringComparison.OrdinalIgnoreCase) >= 0)
                return 1;
            if (leaf.IndexOf("hips", StringComparison.OrdinalIgnoreCase) >= 0
                || leaf.IndexOf("pelvis", StringComparison.OrdinalIgnoreCase) >= 0)
                return 2;
            return 3;
        }

        static int PathDepth(string path)
        {
            if (string.IsNullOrEmpty(path))
                return 0;
            int depth = 1;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == '/')
                    depth++;
            }
            return depth;
        }
    }
}

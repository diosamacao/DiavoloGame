using System;
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
        int count = Mathf.Max(2, Mathf.CeilToInt(duration * Mathf.Max(1f, samplesPerSecond)) + 1);
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

        return LocomotionRootMotionTrack.Create(duration, positions, yaws);
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
        AnimationCurve localX = null, localY = null, localZ = null;
        AnimationCurve localEx = null, localEy = null, localEz = null;
        AnimationCurve localQx = null, localQy = null, localQz = null, localQw = null;

        foreach (EditorCurveBinding binding in bindings)
        {
            string prop = binding.propertyName;
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null)
                continue;

            switch (prop)
            {
                case "RootT.x": humanX = curve; break;
                case "RootT.y": humanY = curve; break;
                case "RootT.z": humanZ = curve; break;
                case "RootQ.x": humanQx = curve; break;
                case "RootQ.y": humanQy = curve; break;
                case "RootQ.z": humanQz = curve; break;
                case "RootQ.w": humanQw = curve; break;
                case "m_LocalPosition.x": localX = curve; break;
                case "m_LocalPosition.y": localY = curve; break;
                case "m_LocalPosition.z": localZ = curve; break;
                case "localEulerAnglesRaw.x":
                case "localEulerAnglesBaked.x":
                case "m_LocalEulerAngles.x":
                    localEx = curve; break;
                case "localEulerAnglesRaw.y":
                case "localEulerAnglesBaked.y":
                case "m_LocalEulerAngles.y":
                    localEy = curve; break;
                case "localEulerAnglesRaw.z":
                case "localEulerAnglesBaked.z":
                case "m_LocalEulerAngles.z":
                    localEz = curve; break;
                case "m_LocalRotation.x": localQx = curve; break;
                case "m_LocalRotation.y": localQy = curve; break;
                case "m_LocalRotation.z": localQz = curve; break;
                case "m_LocalRotation.w": localQw = curve; break;
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

        if (localX != null || localZ != null)
        {
            posX = localX;
            posY = localY;
            posZ = localZ;
            if (localEy != null)
            {
                rotY = localEy;
                rotX = localEx;
                rotZ = localEz;
                rotationIsEulerY = true;
            }
            else
            {
                rotX = localQx;
                rotY = localQy;
                rotZ = localQz;
                rotW = localQw;
                rotationIsEulerY = false;
            }

            return true;
        }

        return false;
    }
}

using UnityEngine;

/// <summary>世界空间定向包围盒；HalfExtents 为各轴半长。</summary>
public readonly struct HitboxOrientedBox
{
    public HitboxOrientedBox(Vector3 center, Vector3 halfExtents, Quaternion rotation)
    {
        Center = center;
        HalfExtents = halfExtents;
        Rotation = rotation;
    }

    public Vector3 Center { get; }
    public Vector3 HalfExtents { get; }
    public Quaternion Rotation { get; }

    /// <summary>按 local 轴索引返回世界空间轴向（0=Right, 1=Up, 2=Forward）。</summary>
    public Vector3 GetAxis(int index)
    {
        return index switch
        {
            0 => Rotation * Vector3.right,
            1 => Rotation * Vector3.up,
            _ => Rotation * Vector3.forward,
        };
    }
}

/// <summary>Hitbox / Hurtbox 逻辑坐标 OBB 构建与相交（不依赖 Physics）。</summary>
public static class HitboxMath
{
    const float AxisEpsilon = 1e-6f;

    /// <summary>
    /// 逻辑根位姿 + 相对角色根的挂点局部 TRS + Hitbox 局部位姿 → 世界 OBB。
    /// 运行时权威入口；水平根来自 MotorSim。
    /// </summary>
    public static HitboxOrientedBox BuildFromHitboxLogical(
        in SimCombatPose rootPose,
        Vector3 attachLocalPosition,
        Quaternion attachLocalRotation,
        HitboxNotifyState hitbox)
    {
        if (hitbox == null)
            return default;

        Quaternion hitLocalRot = Quaternion.Euler(hitbox.LocalEulerAngles);
        Vector3 centerLocal = attachLocalPosition + attachLocalRotation * hitbox.LocalOffset;
        Quaternion rotLocal = attachLocalRotation * hitLocalRot;
        Vector3 center = rootPose.TransformPoint(centerLocal);
        Quaternion rotation = rootPose.TransformRotation(rotLocal);
        Vector3 halfExtents = Vector3.Max(hitbox.Size * 0.5f, Vector3.one * 0.01f);
        return new HitboxOrientedBox(center, halfExtents, rotation);
    }

    /// <summary>逻辑根位姿 + HurtboxDefinition（相对角色根）→ 世界 OBB。</summary>
    public static HitboxOrientedBox BuildFromHurtboxLogical(
        in SimCombatPose rootPose,
        HurtboxDefinition hurtbox)
    {
        if (hurtbox == null)
            return default;

        Quaternion localRotation = Quaternion.Euler(hurtbox.LocalEulerAngles);
        Vector3 center = rootPose.TransformPoint(hurtbox.LocalOffset);
        Quaternion rotation = rootPose.TransformRotation(localRotation);
        Vector3 halfExtents = Vector3.Max(hurtbox.Size * 0.5f, Vector3.one * 0.01f);
        return new HitboxOrientedBox(center, halfExtents, rotation);
    }

    /// <summary>Editor / Gizmo：由 Transform 挂点构建（非运行时权威）。</summary>
    public static HitboxOrientedBox BuildFromHitbox(Transform root, Transform attachPoint, HitboxNotifyState hitbox)
    {
        Transform anchor = attachPoint != null ? attachPoint : root;
        if (anchor == null || hitbox == null || root == null)
            return default;

        // 转为相对角色根的局部，再按根 Transform 构图，与逻辑路径公式一致
        Vector3 attachLocalPos = root.InverseTransformPoint(anchor.position);
        Quaternion attachLocalRot = Quaternion.Inverse(root.rotation) * anchor.rotation;
        var pose = new SimCombatPose(root.position, root.eulerAngles.y);
        return BuildFromHitboxLogical(in pose, attachLocalPos, attachLocalRot, hitbox);
    }

    /// <summary>Editor / Gizmo：由 Transform 根构建 Hurtbox。</summary>
    public static HitboxOrientedBox BuildFromHurtbox(Transform root, HurtboxDefinition hurtbox)
    {
        if (root == null || hurtbox == null)
            return default;

        var pose = new SimCombatPose(root.position, root.eulerAngles.y);
        return BuildFromHurtboxLogical(in pose, hurtbox);
    }

    /// <summary>两 OBB 是否相交（分离轴定理）。</summary>
    public static bool Intersects(HitboxOrientedBox a, HitboxOrientedBox b)
    {
        Vector3 t = b.Center - a.Center;

        if (!TestSeparatingAxis(t, a.GetAxis(0), a, b))
            return false;
        if (!TestSeparatingAxis(t, a.GetAxis(1), a, b))
            return false;
        if (!TestSeparatingAxis(t, a.GetAxis(2), a, b))
            return false;

        if (!TestSeparatingAxis(t, b.GetAxis(0), a, b))
            return false;
        if (!TestSeparatingAxis(t, b.GetAxis(1), a, b))
            return false;
        if (!TestSeparatingAxis(t, b.GetAxis(2), a, b))
            return false;

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Vector3 axis = Vector3.Cross(a.GetAxis(i), b.GetAxis(j));
                if (axis.sqrMagnitude < AxisEpsilon)
                    continue;

                if (!TestSeparatingAxis(t, axis.normalized, a, b))
                    return false;
            }
        }

        return true;
    }

    /// <summary>在指定轴上投影两 OBB，若区间不重叠则该轴为分离轴。</summary>
    static bool TestSeparatingAxis(Vector3 translation, Vector3 axis, HitboxOrientedBox a, HitboxOrientedBox b)
    {
        float distance = Mathf.Abs(Vector3.Dot(translation, axis));
        float radius = ProjectRadius(a, axis) + ProjectRadius(b, axis);
        return distance <= radius;
    }

    static float ProjectRadius(HitboxOrientedBox box, Vector3 axis)
    {
        return box.HalfExtents.x * Mathf.Abs(Vector3.Dot(axis, box.GetAxis(0)))
            + box.HalfExtents.y * Mathf.Abs(Vector3.Dot(axis, box.GetAxis(1)))
            + box.HalfExtents.z * Mathf.Abs(Vector3.Dot(axis, box.GetAxis(2)));
    }
}

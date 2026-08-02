using UnityEngine;

/// <summary>
/// 命中用逻辑根位姿：水平来自 MotorSim，高度可暂跟表现根；不读挂点世界 Transform。
/// </summary>
public readonly struct SimCombatPose
{
    /// <summary>世界坐标（米）。</summary>
    public readonly Vector3 Position;

    /// <summary>绕 Y 朝向（度）。</summary>
    public readonly float YawDegrees;

    /// <summary>由世界位置与偏航构造。</summary>
    public SimCombatPose(Vector3 position, float yawDegrees)
    {
        Position = position;
        YawDegrees = yawDegrees;
    }

    /// <summary>仅水平偏航的根旋转。</summary>
    public Quaternion Rotation => Quaternion.Euler(0f, YawDegrees, 0f);

    /// <summary>从 MotorSim 取 XZ/朝向，Y 使用传入高度（通常为角色根当前高度）。</summary>
    public static SimCombatPose FromMotor(CharacterMotorSim motor, float heightY)
    {
        if (motor == null)
            return new SimCombatPose(new Vector3(0f, heightY, 0f), 0f);

        SimVec2 p = motor.PositionMm;
        return new SimCombatPose(
            new Vector3(
                MotionQuantization.MmToMeters(p.X),
                heightY,
                MotionQuantization.MmToMeters(p.Z)),
            MotionQuantization.MilliDegToDegrees(motor.FacingMilliDeg));
    }

    /// <summary>把相对根的局部点变到世界。</summary>
    public Vector3 TransformPoint(Vector3 localPoint) => Position + Rotation * localPoint;

    /// <summary>把相对根的局部旋转变到世界。</summary>
    public Quaternion TransformRotation(Quaternion localRotation) => Rotation * localRotation;
}

/// <summary>Motion Modifier 窗口模式（Wave 4）。</summary>
public enum MotionModifierMode
{
    /// <summary>窗内不参与角色软体互撞（仍碰静态墙）。</summary>
    SoftBodySuppress = 0,

    /// <summary>捕获连线偏移并重映射基础位移，在窗口末帧到达目标落点。</summary>
    TargetAdhesion = 1,

    /// <summary>仅修正朝向（本切片可不实现运行时）。</summary>
    FaceTarget = 2,

    /// <summary>限制与目标距离（本切片可不实现运行时）。</summary>
    ClampTargetDistance = 3,
}

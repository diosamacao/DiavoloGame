using System;

/// <summary>
/// 角色水平位姿的逻辑权威：毫米坐标 + 毫度朝向；位移经碰撞世界解析，不读 Transform/CC。
/// </summary>
public sealed class CharacterMotorSim
{
    public const int DefaultSoftBodyMass = 100;

    readonly ISimCollisionWorld _collision;
    readonly int _radiusMm;
    readonly int _softBodyMass;
    readonly bool _softBodyImmovable;
    int _xMm;
    int _zMm;
    int _facingMilliDeg;

    /// <summary>使用碰撞世界、水平半径与软弹开质量创建电机。</summary>
    public CharacterMotorSim(
        ISimCollisionWorld collision,
        int radiusMm,
        int softBodyMass = DefaultSoftBodyMass,
        bool softBodyImmovable = false)
    {
        _collision = collision ?? throw new ArgumentNullException(nameof(collision));
        _radiusMm = Math.Max(0, radiusMm);
        _softBodyImmovable = softBodyImmovable;
        // 可推动体质量至少为 1，避免除零；不可推动时 Resolve 读 ImmovableMass
        _softBodyMass = softBodyImmovable
            ? SoftBodySeparation.ImmovableMass
            : Math.Max(1, softBodyMass);
    }

    /// <summary>世界水平位置（毫米）。</summary>
    public SimVec2 PositionMm => new(_xMm, _zMm);

    /// <summary>绕 Y 朝向（毫度）。</summary>
    public int FacingMilliDeg => _facingMilliDeg;

    /// <summary>水平碰撞半径（毫米）。</summary>
    public int RadiusMm => _radiusMm;

    /// <summary>软弹开质量；不可推动时为 <see cref="SoftBodySeparation.ImmovableMass"/>。</summary>
    public int SoftBodyMass => _softBodyMass;

    /// <summary>为 true 时软弹开推力全给对方，自身像墙。</summary>
    public bool SoftBodyImmovable => _softBodyImmovable;

    /// <summary>瞬移到世界水平毫米坐标；不经碰撞。</summary>
    public void TeleportMm(int xMm, int zMm)
    {
        _xMm = xMm;
        _zMm = zMm;
    }

    /// <summary>
    /// 写入软弹开后的目标点，再经静态碰撞世界从旧点解析，避免软推穿墙。
    /// </summary>
    public void CommitSoftSeparatedPosition(int xMm, int zMm)
    {
        var from = new SimVec2(_xMm, _zMm);
        var desired = new SimVec2(xMm, zMm);
        SimVec2 resolved = _collision.ResolveMove(from, desired, _radiusMm);
        _xMm = resolved.X;
        _zMm = resolved.Z;
    }

    /// <summary>从米坐标瞬移；用于出生点与 Unity 根对齐。</summary>
    public void TeleportMeters(float xMeters, float zMeters) =>
        TeleportMm(MotionQuantization.MetersToMm(xMeters), MotionQuantization.MetersToMm(zMeters));

    /// <summary>设置朝向毫度（不归一化到 ±180，调用方可先 Wrap）。</summary>
    public void SetFacingMilliDeg(int facingMilliDeg) => _facingMilliDeg = facingMilliDeg;

    /// <summary>从度设置朝向。</summary>
    public void SetFacingDegrees(float yawDegrees) =>
        SetFacingMilliDeg(MotionQuantization.DegreesToMilliDeg(yawDegrees));

    /// <summary>施加世界平面毫米位移；经碰撞解析后写回位置。</summary>
    public bool TryMoveWorldMm(int dxMm, int dzMm)
    {
        if (dxMm == 0 && dzMm == 0)
            return false;

        var from = new SimVec2(_xMm, _zMm);
        var desired = new SimVec2(_xMm + dxMm, _zMm + dzMm);
        SimVec2 resolved = _collision.ResolveMove(from, desired, _radiusMm);
        bool moved = resolved.X != _xMm || resolved.Z != _zMm;
        _xMm = resolved.X;
        _zMm = resolved.Z;
        return moved;
    }

    /// <summary>施加世界平面米位移（量化后走毫米路径）。</summary>
    public bool TryMoveWorldMeters(float dxMeters, float dzMeters) =>
        TryMoveWorldMm(
            MotionQuantization.MetersToMm(dxMeters),
            MotionQuantization.MetersToMm(dzMeters));

    /// <summary>
    /// 角色本地水平 Δ（右=X、前=Z）按当前朝向转到世界后移动。
    /// 烘焙动作表与本地根运动共用此入口。
    /// </summary>
    public bool TryMoveLocalMm(int localXMm, int localZMm)
    {
        if (localXMm == 0 && localZMm == 0)
            return false;

        RotateLocalToWorld(_facingMilliDeg, localXMm, localZMm, out int wx, out int wz);
        return TryMoveWorldMm(wx, wz);
    }

    /// <summary>本地毫米向量按朝向转到世界（Unity Yaw：forward=(sin,0,cos)）。</summary>
    public static void RotateLocalToWorld(
        int facingMilliDeg,
        int localXMm,
        int localZMm,
        out int worldXMm,
        out int worldZMm)
    {
        double radians = facingMilliDeg / (double)MotionQuantization.MilliDegPerDeg * (Math.PI / 180.0);
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        // right=(cos,0,-sin), forward=(sin,0,cos)
        worldXMm = (int)Math.Round(localXMm * cos + localZMm * sin, MidpointRounding.AwayFromZero);
        worldZMm = (int)Math.Round(-localXMm * sin + localZMm * cos, MidpointRounding.AwayFromZero);
    }
}

using System;

/// <summary>
/// 角色位姿逻辑权威：水平毫米 + 竖直毫米 + 毫度朝向；位移经碰撞世界解析，不读 Transform/CC。
/// </summary>
public sealed class CharacterMotorSim
{
    public const int DefaultSoftBodyMass = 100;
    public const int DefaultGravityMmPerSec2 = -20000;
    public const int DefaultGroundedGravityMmPerSec2 = -2000;

    readonly ISimCollisionWorld _collision;
    readonly int _radiusMm;
    readonly int _softBodyMass;
    readonly bool _softBodyImmovable;
    readonly int _logicHz;
    readonly int _gravityMmPerSec2;
    readonly int _groundedGravityMmPerSec2;
    int _xMm;
    int _yMm;
    int _zMm;
    int _facingMilliDeg;
    int _verticalVelocityMmPerSec;
    bool _isGrounded;
    int _softBodySuppressFrames;

    /// <summary>使用碰撞世界、水平半径、软弹开与竖直参数创建电机。</summary>
    public CharacterMotorSim(
        ISimCollisionWorld collision,
        int radiusMm,
        int softBodyMass = DefaultSoftBodyMass,
        bool softBodyImmovable = false,
        int logicHz = SimulationConfig.DefaultLogicHz,
        int gravityMmPerSec2 = DefaultGravityMmPerSec2,
        int groundedGravityMmPerSec2 = DefaultGroundedGravityMmPerSec2)
    {
        _collision = collision ?? throw new ArgumentNullException(nameof(collision));
        _radiusMm = Math.Max(0, radiusMm);
        _softBodyImmovable = softBodyImmovable;
        // 可推动体质量至少为 1，避免除零；不可推动时 Resolve 读 ImmovableMass
        _softBodyMass = softBodyImmovable
            ? SoftBodySeparation.ImmovableMass
            : Math.Max(1, softBodyMass);
        _logicHz = logicHz > 0 ? logicHz : SimulationConfig.DefaultLogicHz;
        _gravityMmPerSec2 = gravityMmPerSec2;
        _groundedGravityMmPerSec2 = groundedGravityMmPerSec2;
        _yMm = _collision.GroundYMm;
        _isGrounded = true;
        _verticalVelocityMmPerSec = _groundedGravityMmPerSec2;
    }

    /// <summary>静物碰撞世界（重定位与位移共用）。</summary>
    public ISimCollisionWorld CollisionWorld => _collision;

    /// <summary>世界水平位置（毫米）。</summary>
    public SimVec2 PositionMm => new(_xMm, _zMm);

    /// <summary>世界竖直位置（毫米）。</summary>
    public int YMm => _yMm;

    /// <summary>绕 Y 朝向（毫度）。</summary>
    public int FacingMilliDeg => _facingMilliDeg;

    /// <summary>水平碰撞半径（毫米）。</summary>
    public int RadiusMm => _radiusMm;

    /// <summary>软弹开质量；不可推动时为 <see cref="SoftBodySeparation.ImmovableMass"/>。</summary>
    public int SoftBodyMass => _softBodyMass;

    /// <summary>为 true 时软弹开推力全给对方，自身像墙。</summary>
    public bool SoftBodyImmovable => _softBodyImmovable;

    /// <summary>剩余软体抑制逻辑帧；&gt;0 时不参与 SoftBodySeparation。</summary>
    public int SoftBodySuppressFrames => _softBodySuppressFrames > 0 ? _softBodySuppressFrames : 0;

    /// <summary>当前是否处于软体抑制。</summary>
    public bool IsSoftBodySuppressed => SoftBodySuppressFrames > 0;

    /// <summary>逻辑着地：竖直位置贴地且未向上跃起。</summary>
    public bool IsGrounded => _isGrounded;

    /// <summary>延长或刷新软体抑制（取较大值）。</summary>
    public void SetSoftBodySuppressFrames(int frames) =>
        _softBodySuppressFrames = Math.Max(_softBodySuppressFrames, Math.Max(0, frames));

    /// <summary>每逻辑步开头递减抑制计数。</summary>
    public void TickSoftBodySuppress()
    {
        if (_softBodySuppressFrames > 0)
            _softBodySuppressFrames--;
    }

    /// <summary>招式结束时清除抑制。</summary>
    public void ClearSoftBodySuppress() => _softBodySuppressFrames = 0;

    /// <summary>竖直速度（毫米/秒）；调试与测试用。</summary>
    public int VerticalVelocityMmPerSec => _verticalVelocityMmPerSec;

    /// <summary>瞬移到世界水平毫米坐标；不经碰撞；Y 不变。</summary>
    public void TeleportMm(int xMm, int zMm)
    {
        _xMm = xMm;
        _zMm = zMm;
    }

    /// <summary>瞬移到世界毫米坐标（含 Y）；不经碰撞。</summary>
    public void TeleportMm(int xMm, int yMm, int zMm)
    {
        _xMm = xMm;
        _yMm = yMm;
        _zMm = zMm;
        RefreshGroundedFromHeight();
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

    /// <summary>从米坐标瞬移水平位置；用于出生点与 Unity 根对齐。</summary>
    public void TeleportMeters(float xMeters, float zMeters) =>
        TeleportMm(MotionQuantization.MetersToMm(xMeters), MotionQuantization.MetersToMm(zMeters));

    /// <summary>从米坐标瞬移含 Y；对齐 Unity 根完整位姿。</summary>
    public void TeleportMeters(float xMeters, float yMeters, float zMeters) =>
        TeleportMm(
            MotionQuantization.MetersToMm(xMeters),
            MotionQuantization.MetersToMm(yMeters),
            MotionQuantization.MetersToMm(zMeters));

    /// <summary>设置朝向毫度（不归一化到 ±180，调用方可先 Wrap）。</summary>
    public void SetFacingMilliDeg(int facingMilliDeg) => _facingMilliDeg = facingMilliDeg;

    /// <summary>从度设置朝向。</summary>
    public void SetFacingDegrees(float yawDegrees) =>
        SetFacingMilliDeg(MotionQuantization.DegreesToMilliDeg(yawDegrees));

    /// <summary>
    /// 固定逻辑帧推进竖直速度与高度；着地时钳到 GroundYMm。
    /// 使用整数 / logicHz，避免 float dt 进入权威路径。
    /// </summary>
    public void TickVertical()
    {
        if (_isGrounded && _verticalVelocityMmPerSec < 0)
            _verticalVelocityMmPerSec = _groundedGravityMmPerSec2;

        _verticalVelocityMmPerSec += _gravityMmPerSec2 / _logicHz;
        _yMm += _verticalVelocityMmPerSec / _logicHz;

        int groundY = _collision.GroundYMm;
        if (_yMm <= groundY)
        {
            _yMm = groundY;
            _isGrounded = true;
            if (_verticalVelocityMmPerSec < 0)
                _verticalVelocityMmPerSec = _groundedGravityMmPerSec2;
        }
        else
        {
            _isGrounded = false;
        }
    }

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

    void RefreshGroundedFromHeight()
    {
        int groundY = _collision.GroundYMm;
        if (_yMm <= groundY)
        {
            _yMm = groundY;
            _isGrounded = true;
            if (_verticalVelocityMmPerSec < 0)
                _verticalVelocityMmPerSec = _groundedGravityMmPerSec2;
        }
        else
        {
            _isGrounded = false;
        }
    }
}

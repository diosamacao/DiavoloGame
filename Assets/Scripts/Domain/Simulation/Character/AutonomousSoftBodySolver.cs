using System;

/// <summary>
/// 客机本机软弹开：不进 <see cref="SimulationWorld"/>，只把自己从只读幽灵圆盘推出。
/// 幽灵按不可推动墙处理，避免改权威表现位姿。
/// </summary>
public static class AutonomousSoftBodySolver
{
    static SimVec2[] _positions = Array.Empty<SimVec2>();
    static int[] _radiiMm = Array.Empty<int>();
    static int[] _masses = Array.Empty<int>();

    /// <summary>
    /// 若本机与任一阻挡圆盘重叠则推开本机并写入 MotorSim。
    /// 本机 SoftBody 抑制时不推。阻挡圆盘位置不变。
    /// </summary>
    public static bool TrySeparateLocal(
        CharacterMotorSim local,
        SimVec2[] blockerPositionsMm,
        int[] blockerRadiiMm,
        int blockerCount,
        int factorMilli = SimulationConfig.DefaultSoftSeparationFactorMilli,
        int iterations = SimulationConfig.DefaultSoftSeparationIterations)
    {
        if (local == null
            || local.IsSoftBodySuppressed
            || blockerPositionsMm == null
            || blockerRadiiMm == null
            || blockerCount <= 0
            || blockerCount > blockerPositionsMm.Length
            || blockerCount > blockerRadiiMm.Length
            || factorMilli <= 0
            || iterations <= 0)
        {
            return false;
        }

        int count = blockerCount + 1;
        EnsureBuffers(count);
        _positions[0] = local.PositionMm;
        _radiiMm[0] = local.RadiusMm;
        _masses[0] = local.SoftBodyMass <= SoftBodySeparation.ImmovableMass
            ? 1
            : local.SoftBodyMass;
        for (int i = 0; i < blockerCount; i++)
        {
            _positions[i + 1] = blockerPositionsMm[i];
            _radiiMm[i + 1] = blockerRadiiMm[i];
            _masses[i + 1] = SoftBodySeparation.ImmovableMass;
        }

        SimVec2 before = _positions[0];
        SoftBodySeparation.Resolve(
            _positions,
            _radiiMm,
            _masses,
            count,
            factorMilli,
            iterations);

        if (_positions[0].X == before.X && _positions[0].Z == before.Z)
            return false;

        local.CommitSoftSeparatedPosition(_positions[0].X, _positions[0].Z);
        return true;
    }

    static void EnsureBuffers(int count)
    {
        if (_positions.Length >= count)
            return;

        _positions = new SimVec2[count];
        _radiiMm = new int[count];
        _masses = new int[count];
    }
}

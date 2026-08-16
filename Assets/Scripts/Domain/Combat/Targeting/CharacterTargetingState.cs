using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>每逻辑帧自动维护角色唯一 SelectedTarget，并消费确定性切敌边沿。</summary>
public sealed class CharacterTargetingState
{
    readonly int _teamId;
    readonly int _acquireRangeMm;
    readonly int _retainRangeMm;
    readonly Func<IReadOnlyList<IHurtboxTarget>> _targetsProvider;
    readonly List<SimTargetCandidate> _candidateScratch = new(16);
    SimActorId _selectedTargetId;

    /// <summary>创建目标状态；范围单位为毫米。</summary>
    public CharacterTargetingState(
        int teamId,
        int acquireRangeMm,
        int retainRangeMm,
        Func<IReadOnlyList<IHurtboxTarget>> targetsProvider)
    {
        _teamId = teamId;
        _acquireRangeMm = Math.Max(0, acquireRangeMm);
        _retainRangeMm = Math.Max(_acquireRangeMm, retainRangeMm);
        _targetsProvider = targetsProvider;
    }

    /// <summary>当前只读目标快照。</summary>
    public CharacterTargetingSnapshot Snapshot =>
        new CharacterTargetingSnapshot(_selectedTargetId);

    /// <summary>阵营 Id，供复制快照填写。</summary>
    public int TeamId => _teamId;

    /// <summary>在动作解析前推进自动选择与左右切敌；Action/Locomotion 共用同一规则。</summary>
    public void Step(
        SimActorId requesterId,
        CharacterMotorSim requesterMotor,
        in InputFrame inputFrame)
    {
        if (!requesterId.IsValid || requesterMotor == null)
        {
            _selectedTargetId = SimActorId.Invalid;
            return;
        }

        // 从已提交逻辑 Pose 建候选，禁止读 Transform
        BuildCandidateSnapshot();
        // 左右切敌边沿；同帧双按视为不切
        TargetSwitchDirection switchDirection = ResolveSwitchDirection(in inputFrame);
        var request = new SimTargetResolveRequest(
            requesterId,
            _teamId,
            requesterMotor.PositionMm.X,
            requesterMotor.PositionMm.Z,
            inputFrame.MoveReferenceYawQuantized,
            _acquireRangeMm,
            _retainRangeMm,
            _selectedTargetId,
            switchDirection);
        // 自动维持 / 切敌：写出本帧唯一 SelectedTarget
        _selectedTargetId = DeterministicTargetResolver.Resolve(in request, _candidateScratch);
    }

    /// <summary>读取当前目标的逻辑 Pose；目标已注销时返回 false。</summary>
    public bool TryGetSelectedCombatPose(out SimCombatPose pose)
    {
        pose = default;
        if (!_selectedTargetId.IsValid)
            return false;

        IReadOnlyList<IHurtboxTarget> targets = _targetsProvider?.Invoke();
        if (targets == null)
            return false;

        for (int i = 0; i < targets.Count; i++)
        {
            IHurtboxTarget target = targets[i];
            if (target == null || target.SimulationId != _selectedTargetId)
                continue;

            pose = target.GetLogicalCombatPose();
            return true;
        }

        return false;
    }

    /// <summary>按逻辑 Pose 计算从角色到当前目标的世界水平方向。</summary>
    public bool TryGetSelectedDirection(CharacterMotorSim requesterMotor, out Vector3 direction)
    {
        direction = Vector3.zero;
        if (requesterMotor == null || !TryGetSelectedCombatPose(out SimCombatPose pose))
            return false;

        int targetXMm = MotionQuantization.MetersToMm(pose.Position.x);
        int targetZMm = MotionQuantization.MetersToMm(pose.Position.z);
        int dxMm = targetXMm - requesterMotor.PositionMm.X;
        int dzMm = targetZMm - requesterMotor.PositionMm.Z;
        if (dxMm == 0 && dzMm == 0)
            return false;

        direction = new Vector3(dxMm, 0f, dzMm).normalized;
        return true;
    }

    /// <summary>把 SelectedTargetId 映射到表现目标；Camera/UI 只读使用。</summary>
    public bool TryGetSelectedTarget(out ITargetable selected)
    {
        selected = null;
        if (!_selectedTargetId.IsValid)
            return false;

        IReadOnlyList<IHurtboxTarget> targets = _targetsProvider?.Invoke();
        if (targets == null)
            return false;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] is not ITargetable target
                || target.SimulationId != _selectedTargetId
                || !target.IsAlive)
            {
                continue;
            }

            selected = target;
            return true;
        }

        return false;
    }

    /// <summary>从已提交逻辑 Pose 构建无 Transform 的确定性候选列表。</summary>
    void BuildCandidateSnapshot()
    {
        _candidateScratch.Clear();
        IReadOnlyList<IHurtboxTarget> targets = _targetsProvider?.Invoke();
        if (targets == null)
            return;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] is not ITargetable target)
                continue;

            SimCombatPose pose = target.GetLogicalCombatPose();
            _candidateScratch.Add(new SimTargetCandidate(
                target.SimulationId,
                target.TeamId,
                MotionQuantization.MetersToMm(pose.Position.x),
                MotionQuantization.MetersToMm(pose.Position.z),
                target.IsAlive));
        }
    }

    static TargetSwitchDirection ResolveSwitchDirection(in InputFrame inputFrame)
    {
        bool left = inputFrame.WasPressed(InputButton.TargetSwitchLeft);
        bool right = inputFrame.WasPressed(InputButton.TargetSwitchRight);
        if (left == right)
            return TargetSwitchDirection.None;
        return left ? TargetSwitchDirection.Left : TargetSwitchDirection.Right;
    }
}

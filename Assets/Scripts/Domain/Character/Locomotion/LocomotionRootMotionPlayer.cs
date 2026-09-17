using UnityEngine;

/// <summary>按烘焙轨 + 整数逻辑帧推进 Stop/Pivot 根位移；不读动画 NormalizedTime。</summary>
public sealed class LocomotionRootMotionPlayer
{
    readonly CharacterLocomotionProfile _profile;

    LocomotionRootMotionTrack _track;
    AnimationKey _key;
    bool _active;
    Quaternion _basisRotation = Quaternion.identity;

    public LocomotionRootMotionPlayer(CharacterLocomotionProfile profile)
    {
        _profile = profile;
    }

    public bool IsActive => _active && _track.IsValid;

    /// <summary>开始一段根位移会话；basis 为进入相位时的角色朝向（用于局部→世界）。</summary>
    public void Begin(AnimationKey key, Quaternion basisRotation)
    {
        _key = key;
        _track = _profile != null ? _profile.GetRootMotionTrack(key) : LocomotionRootMotionTrack.Empty;
        _active = _track.IsValid && _profile != null && _profile.IsRootMotionEnabled(key);
        _basisRotation = basisRotation;
        Vector3 forward = _basisRotation * Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f)
            _basisRotation = Quaternion.LookRotation(forward.normalized);
    }

    /// <summary>结束会话。</summary>
    public void End()
    {
        _active = false;
        _track = LocomotionRootMotionTrack.Empty;
    }

    /// <summary>捕获烘焙会话身份与朝向基；帧游标唯一来自 Locomotion PhaseFrame。</summary>
    public void Capture(out bool active, out AnimationKey key, out float basisYaw)
    {
        active = _active;
        key = _key;
        basisYaw = _basisRotation.eulerAngles.y;
    }

    /// <summary>恢复烘焙会话；实际采样帧由外部 PhaseFrame 提供。</summary>
    public void Restore(bool active, AnimationKey key, float basisYaw)
    {
        if (!active)
        {
            End();
            return;
        }

        Begin(key, Quaternion.Euler(0f, basisYaw, 0f));
    }

    /// <summary>
    /// 直接采样指定 PhaseFrame 的位移，不维护第二套游标。
    /// applyYaw 为 false 时忽略烘焙偏航（转身 Clip 已含骨骼转向时使用）。
    /// </summary>
    public bool TrySample(
        int phaseFrame,
        bool applyYaw,
        out Vector3 worldDelta,
        out float yawDeltaDegrees)
    {
        worldDelta = Vector3.zero;
        yawDeltaDegrees = 0f;
        if (!IsActive)
            return false;

        int frameCount = _track.GetFrameCount(ActionSim.LogicHz);
        int frame = Mathf.Max(0, phaseFrame);
        if (frame >= frameCount)
            return false;

        if (!_track.TryGetFrameDelta(
                frame,
                ActionSim.LogicHz,
                out Vector3 localDelta,
                out float yawDelta))
        {
            return false;
        }

        float scale = _profile != null ? _profile.RootMotionPositionScale : 1f;
        localDelta.y = 0f;
        worldDelta = _basisRotation * localDelta * scale;
        yawDeltaDegrees = applyYaw ? yawDelta : 0f;
        return worldDelta.sqrMagnitude > 0.0000001f || Mathf.Abs(yawDeltaDegrees) > 0.0001f;
    }
}

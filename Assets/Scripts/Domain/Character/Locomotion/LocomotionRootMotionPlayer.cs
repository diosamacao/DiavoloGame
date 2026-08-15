using UnityEngine;

/// <summary>按烘焙轨 + 整数逻辑帧推进 Stop/Pivot 根位移；不读动画 NormalizedTime。</summary>
public sealed class LocomotionRootMotionPlayer
{
    readonly CharacterLocomotionProfile _profile;

    LocomotionRootMotionTrack _track;
    AnimationKey _key;
    bool _active;
    int _frame;
    Quaternion _basisRotation = Quaternion.identity;

    public LocomotionRootMotionPlayer(CharacterLocomotionProfile profile)
    {
        _profile = profile;
    }

    public bool IsActive => _active && _track.IsValid;

    /// <summary>当前会话已消费的逻辑帧索引（下一帧将读取的表下标）。</summary>
    public int CurrentFrame => _frame;

    /// <summary>开始一段根位移会话；basis 为进入相位时的角色朝向（用于局部→世界）。</summary>
    public void Begin(AnimationKey key, Quaternion basisRotation)
    {
        _key = key;
        _track = _profile != null ? _profile.GetRootMotionTrack(key) : LocomotionRootMotionTrack.Empty;
        _active = _track.IsValid && _profile != null && _profile.IsRootMotionEnabled(key);
        _frame = 0;
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
        _frame = 0;
        _track = LocomotionRootMotionTrack.Empty;
    }

    /// <summary>捕获烘焙游标，供 SavedState。</summary>
    public void Capture(out bool active, out AnimationKey key, out int frame, out float basisYaw)
    {
        active = _active;
        key = _key;
        frame = _frame;
        basisYaw = _basisRotation.eulerAngles.y;
    }

    /// <summary>恢复烘焙会话；inactive 时 End。frame 为下一帧将读取的表下标。</summary>
    public void Restore(bool active, AnimationKey key, int frame, float basisYaw)
    {
        if (!active)
        {
            End();
            return;
        }

        Begin(key, Quaternion.Euler(0f, basisYaw, 0f));
        int frameCount = _track.IsValid ? _track.GetFrameCount(ActionSim.LogicHz) : 0;
        _frame = frameCount <= 0 ? 0 : Mathf.Clamp(frame, 0, frameCount);
    }

    /// <summary>
    /// 消费当前逻辑帧位移并推进帧索引。
    /// applyYaw 为 false 时忽略烘焙偏航（转身 Clip 已含骨骼转向时使用）。
    /// </summary>
    public bool TryConsume(
        bool applyYaw,
        out Vector3 worldDelta,
        out float yawDeltaDegrees)
    {
        worldDelta = Vector3.zero;
        yawDeltaDegrees = 0f;
        if (!IsActive)
            return false;

        int frameCount = _track.GetFrameCount(ActionSim.LogicHz);
        if (_frame >= frameCount)
            return false;

        if (!_track.TryGetFrameDelta(
                _frame,
                ActionSim.LogicHz,
                out Vector3 localDelta,
                out float yawDelta))
        {
            return false;
        }

        _frame++;
        float scale = _profile != null ? _profile.RootMotionPositionScale : 1f;
        localDelta.y = 0f;
        worldDelta = _basisRotation * localDelta * scale;
        yawDeltaDegrees = applyYaw ? yawDelta : 0f;
        return worldDelta.sqrMagnitude > 0.0000001f || Mathf.Abs(yawDeltaDegrees) > 0.0001f;
    }
}

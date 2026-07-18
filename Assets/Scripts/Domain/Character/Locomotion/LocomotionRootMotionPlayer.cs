using UnityEngine;

/// <summary>按烘焙轨 + 播放归一化时间推进 Stop/Pivot 的脚本根位移（方案 B）。</summary>
public sealed class LocomotionRootMotionPlayer
{
    readonly CharacterLocomotionProfile _profile;

    LocomotionRootMotionTrack _track;
    AnimationKey _key;
    bool _active;
    float _lastTime = -1f;
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
        _lastTime = -1f;
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
        _lastTime = -1f;
        _track = LocomotionRootMotionTrack.Empty;
    }

    /// <summary>
    /// 按当前归一化时间消费位移；首帧只对齐采样点不位移。
    /// applyYaw 为 false 时忽略烘焙偏航（转身 Clip 已含骨骼转向时使用）。
    /// </summary>
    public bool TryConsume(
        float normalizedTime,
        bool applyYaw,
        out Vector3 worldDelta,
        out float yawDeltaDegrees)
    {
        worldDelta = Vector3.zero;
        yawDeltaDegrees = 0f;
        if (!IsActive)
            return false;

        float t = Mathf.Clamp01(normalizedTime) * _track.Duration;
        if (_lastTime < 0f)
        {
            _lastTime = t;
            return false;
        }

        if (!_track.TryGetDelta(_lastTime, t, out Vector3 localDelta, out float yawDelta))
        {
            _lastTime = t;
            return false;
        }

        _lastTime = t;
        float scale = _profile != null ? _profile.RootMotionPositionScale : 1f;
        localDelta.y = 0f;
        worldDelta = _basisRotation * localDelta * scale;
        yawDeltaDegrees = applyYaw ? yawDelta : 0f;
        return worldDelta.sqrMagnitude > 0.0000001f || Mathf.Abs(yawDeltaDegrees) > 0.0001f;
    }
}

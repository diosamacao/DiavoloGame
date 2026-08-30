using UnityEngine;

/// <summary>日常相机锚点唯一写入器：滤左右、FollowHold、Orbit 与 Pitch。</summary>
[DisallowMultipleComponent]
public sealed class CameraRig : AppControllerBase
{
    const float SnapDistance = 3f;

    Transform _cameraRoot;
    Transform _orbitPivot;
    Transform _pitchPivot;
    Vector3 _followVelocity;
    Vector3 _followAnchorPosition;
    bool _positionInitialized;
    bool _followHeld;
    bool _recoveringFromHold;
    Vector3 _heldFollowPosition;

    /// <summary>滤左右与 Hold 后的最终 FollowAnchor 世界位置。</summary>
    public Vector3 FollowAnchorPosition => _followAnchorPosition;

    /// <summary>当前是否钉住 FollowAnchor。</summary>
    public bool IsFollowHeld => _followHeld;

    /// <summary>绑定由 CameraManager 创建的锚点层级。</summary>
    public void Bind(Transform cameraRoot, Transform orbitPivot, Transform pitchPivot)
    {
        _cameraRoot = cameraRoot;
        _orbitPivot = orbitPivot;
        _pitchPivot = pitchPivot;
    }

    /// <summary>在当前世界位置钉住 FollowAnchor；重复调用不会重采样位置。</summary>
    public void SetFollowHold()
    {
        if (_followHeld)
            return;

        _heldFollowPosition = _positionInitialized
            ? _followAnchorPosition
            : _cameraRoot != null ? _cameraRoot.position : transform.position;
        _followHeld = true;
        _followVelocity = Vector3.zero;
    }

    /// <summary>解除 FollowHold；后续帧从钉点平滑追回 CameraRoot。</summary>
    public void ClearFollowHold()
    {
        _followHeld = false;
        _recoveringFromHold = true;
        _followVelocity = Vector3.zero;
    }

    /// <summary>按角色朝向吸收位移并写入 Orbit/Pitch；Hold 时只写钉点姿态。</summary>
    public void Sync(
        Transform followBasis,
        float yaw,
        float pitch,
        float followSmoothTime,
        float lateralFollowFactor,
        Vector3 fallbackForward)
    {
        if (_orbitPivot == null || _pitchPivot == null || _cameraRoot == null)
            return;

        Vector3 source = _cameraRoot.position;
        if (!_positionInitialized)
        {
            SnapToTarget();
        }
        else if (_followHeld)
        {
            _followAnchorPosition = _heldFollowPosition;
            _orbitPivot.position = _heldFollowPosition;
        }
        else if (!_recoveringFromHold
            && (_followAnchorPosition - source).sqrMagnitude > SnapDistance * SnapDistance)
        {
            SnapToTarget();
        }
        else
        {
            Vector3 forward = ResolveForwardAxis(followBasis, _cameraRoot, fallbackForward);
            Vector3 delta = source - _followAnchorPosition;
            Vector3 forwardPart = Vector3.Dot(delta, forward) * forward;
            Vector3 verticalPart = new(0f, delta.y, 0f);
            Vector3 lateralPart = delta - forwardPart - verticalPart;
            Vector3 desired = _followAnchorPosition
                + forwardPart
                + verticalPart
                + lateralPart * Mathf.Clamp01(lateralFollowFactor);

            _followAnchorPosition = followSmoothTime <= 0f
                ? desired
                : Vector3.SmoothDamp(
                    _followAnchorPosition,
                    desired,
                    ref _followVelocity,
                    followSmoothTime);
            _orbitPivot.position = _followAnchorPosition;
            if (_recoveringFromHold
                && (_followAnchorPosition - source).sqrMagnitude < 0.0004f)
            {
                _recoveringFromHold = false;
            }
        }

        _orbitPivot.rotation = Quaternion.Euler(0f, yaw, 0f);
        _pitchPivot.localPosition = Vector3.zero;
        _pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    /// <summary>立刻把 FollowAnchor 吸附到 CameraRoot。</summary>
    public void SnapToTarget()
    {
        if (_orbitPivot == null || _cameraRoot == null)
            return;

        _followAnchorPosition = _cameraRoot.position;
        _orbitPivot.position = _followAnchorPosition;
        _followVelocity = Vector3.zero;
        _positionInitialized = true;
        _recoveringFromHold = false;
        if (_followHeld)
            _heldFollowPosition = _followAnchorPosition;
    }

    /// <summary>解析滤左右水平轴；角色轴退化时使用日常 Orbit 前向。</summary>
    public static Vector3 ResolveForwardAxis(
        Transform followBasis,
        Transform cameraRoot,
        Vector3 fallbackForward)
    {
        Transform basis = followBasis != null ? followBasis : cameraRoot;
        Vector3 forward = basis != null ? basis.forward : fallbackForward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            fallbackForward.y = 0f;
            return fallbackForward.sqrMagnitude > 0.0001f
                ? fallbackForward.normalized
                : Vector3.forward;
        }

        return forward.normalized;
    }
}

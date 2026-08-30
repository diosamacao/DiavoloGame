using Cinemachine;
using UnityEngine;

/// <summary>战斗相机唯一抢权入口：维护模式栈并租用 SkillShot VCam。</summary>
[DisallowMultipleComponent]
public sealed class CameraDirector : AppControllerBase
{
    /// <summary>Ping-Pong 池中的 VCam 与独立 LookAt 世界点。</summary>
    sealed class ShotSlot
    {
        public CinemachineVirtualCamera Camera;
        public Transform LookAt;
    }

    const int FreePriority = 10;
    const int LockOnPriority = 20;
    const int SkillShotPriority = 80;

    readonly CameraDirectorStack _stack = new();
    readonly ShotSlot[] _shotSlots = new ShotSlot[2];

    CameraManager _manager;
    CinemachineBrain _brain;
    CinemachineVirtualCamera _activeShotCamera;
    CameraShotNotifyState _activeShot;
    int _activeShotSlot = -1;
    bool _targetAvailable;
    bool _cameraLockEnabled;
    bool _hasBlendBeforeSkill;
    CinemachineBlendDefinition _blendBeforeSkill;

    /// <summary>纯表现 Camera Lock；SelectedTarget 无效时必为 false。</summary>
    public bool CameraLockEnabled => _cameraLockEnabled;

    /// <summary>当前导演模式。</summary>
    public CameraMode ActiveMode => _stack.Active.Mode;

    /// <summary>绑定唯一 CameraManager 与 Main Camera Brain。</summary>
    public void Bind(CameraManager manager)
    {
        _manager = manager;
        if (_brain == null)
            _brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        ApplyPriorities();
    }

    /// <summary>SelectedTarget 变化时收敛 CameraLock，禁止锁住空目标。</summary>
    public void SetTargetAvailable(bool available)
    {
        _targetAvailable = available;
        if (!available)
            SetCameraLock(false);
    }

    /// <summary>按本地输入切换 CameraLock；无目标时开启请求无效。</summary>
    public void ToggleCameraLock() => SetCameraLock(!_cameraLockEnabled);

    /// <summary>显式设置 CameraLock 并更新导演栈。</summary>
    public void SetCameraLock(bool enabled)
    {
        _cameraLockEnabled = enabled && _targetAvailable;
        if (_cameraLockEnabled)
            _stack.Push(CameraMode.LockOn, LockOnPriority);
        else
            _stack.Remove(CameraMode.LockOn);
        ApplyPriorities();
    }

    /// <summary>应用已由共享求值器生成的世界 Pose；换窗时在两个 CM2 VCam 间 Ping-Pong。</summary>
    public void SetSkillShot(CameraShotNotifyState shot, in CameraShotPose pose)
    {
        if (shot == null || !shot.OverrideCameraPose)
            return;

        bool enteringShot = !ReferenceEquals(_activeShot, shot);
        if (enteringShot)
        {
            _activeShotSlot = (_activeShotSlot + 1) % _shotSlots.Length;
            _activeShotCamera = GetOrCreateShotSlot(_activeShotSlot).Camera;
            _activeShotCamera.m_Transitions.m_InheritPosition = shot.InheritPosition;
            _activeShot = shot;
        }

        ShotSlot slot = _shotSlots[_activeShotSlot];
        Vector3 lookDirection = pose.WorldLookAt - pose.WorldPosition;
        Quaternion rotation = lookDirection.sqrMagnitude > 0.000001f
            ? Quaternion.LookRotation(lookDirection, Vector3.up)
            : _activeShotCamera.transform.rotation;
        _activeShotCamera.transform.SetPositionAndRotation(pose.WorldPosition, rotation);
        slot.LookAt.position = pose.WorldLookAt;
        _activeShotCamera.LookAt = slot.LookAt;
        _activeShotCamera.m_Lens.FieldOfView = pose.FieldOfView;
        _activeShotCamera.ForceCameraPosition(pose.WorldPosition, rotation);

        if (_brain != null)
        {
            if (!_hasBlendBeforeSkill)
            {
                _blendBeforeSkill = _brain.m_DefaultBlend;
                _hasBlendBeforeSkill = true;
            }
            if (enteringShot)
            {
                _brain.m_DefaultBlend = new CinemachineBlendDefinition(
                    CinemachineBlendDefinition.Style.EaseInOut,
                    shot.BlendInSeconds);
            }
        }

        _stack.Push(CameraMode.SkillShot, SkillShotPriority);
        ApplyPriorities();
    }

    /// <summary>清理 SkillShot，回写最后演出水平角并恢复 Gameplay Look。</summary>
    public void ClearSkillShot(CameraRestoreMode restoreMode)
    {
        if (_activeShotCamera != null && _manager != null)
            _manager.SnapshotOrbitYaw(_activeShotCamera.State.FinalOrientation.eulerAngles.y);

        _stack.Remove(CameraMode.SkillShot);
        if (_brain != null && _hasBlendBeforeSkill)
            _brain.m_DefaultBlend = _blendBeforeSkill;
        _hasBlendBeforeSkill = false;
        if (restoreMode == CameraRestoreMode.ForceFree)
            SetCameraLock(false);
        else if (restoreMode == CameraRestoreMode.ForceLockOn)
            SetCameraLock(true);

        _activeShotCamera = null;
        _activeShot = null;
        ApplyPriorities();
    }

    /// <summary>统一门控日常 Look；关闭时 CameraManager 仍持续提交最后 staged yaw。</summary>
    public void SetGameplayLookEnabled(bool enabled)
    {
        _manager?.SetLookEnabled(enabled);
    }

    /// <summary>创建或获取固定的 CM2 Ping-Pong VCam；不按招式或预设 Key 建池。</summary>
    ShotSlot GetOrCreateShotSlot(int index)
    {
        ShotSlot existing = _shotSlots[index];
        if (existing?.Camera != null && existing.LookAt != null)
            return existing;

        var go = new GameObject($"CM SkillShot {index + 1}");
        go.transform.SetParent(transform, false);
        var vcam = go.AddComponent<CinemachineVirtualCamera>();
        vcam.AddCinemachineComponent<CinemachineHardLookAt>();
        vcam.Priority = 0;

        var lookAtObject = new GameObject($"CameraShotLookAt {index + 1}");
        lookAtObject.transform.SetParent(transform, false);
        var slot = new ShotSlot
        {
            Camera = vcam,
            LookAt = lookAtObject.transform,
        };
        vcam.LookAt = slot.LookAt;
        _shotSlots[index] = slot;

        CameraShakeController shake = GetComponent<CameraShakeController>();
        if (shake != null)
            shake.BindVirtualCamera(vcam);
        return slot;
    }

    /// <summary>把栈状态映射为 Free/SkillShot VCam Priority。</summary>
    void ApplyPriorities()
    {
        if (_manager != null && _manager.VirtualCamera != null)
            _manager.VirtualCamera.Priority = FreePriority;

        for (int i = 0; i < _shotSlots.Length; i++)
        {
            CinemachineVirtualCamera shotCamera = _shotSlots[i]?.Camera;
            if (shotCamera != null)
            {
                shotCamera.Priority = shotCamera == _activeShotCamera
                    && _stack.Contains(CameraMode.SkillShot)
                    ? SkillShotPriority
                    : 0;
            }
        }
    }
}

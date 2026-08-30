using UnityEngine;

/// <summary>只读本机 ActionSim 帧并驱动 Camera Timeline；不进入模拟或快照。</summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class CameraShotPlayer : AppControllerBase
{
    CameraManager _manager;
    CameraDirector _director;
    CameraFeedback _feedback;
    CameraShotNotifyState _activeShot;
    ActionDefinition _activeAction;
    int _activeInstanceId;
    CameraReferencePose _referenceSnapshot;
    CameraReferencePose _lookAtSnapshot;
    bool _hasReferenceSnapshot;
    bool _hasLookAtSnapshot;
    bool _skillShotLive;

    void Awake()
    {
        _manager = GetComponent<CameraManager>();
        _director = GetComponent<CameraDirector>();
        _feedback = GetComponent<CameraFeedback>();
    }

    void LateUpdate()
    {
        ILocalPlayer local = _manager != null ? _manager.LocalPlayer : null;
        ActionSim actionSim = local?.Actor?.ActionSim;
        ActionSimSnapshot snapshot = actionSim != null ? actionSim.Snapshot : default;
        ActionDefinition action = snapshot.IsActive ? snapshot.Content as ActionDefinition : null;
        if (action == null)
        {
            ClearActiveShot();
            return;
        }

        CameraShotNotifyState shot = action.GetActiveCameraShotAtFrame(snapshot.CurrentFrame);
        bool actionChanged = action != _activeAction || snapshot.InstanceId != _activeInstanceId;
        if (actionChanged)
            ClearActiveShot();

        _activeAction = action;
        _activeInstanceId = snapshot.InstanceId;
        if (shot == null)
        {
            ClearShotOnly(action.Timeline.CameraSettings.RestoreMode);
            return;
        }

        if (!ReferenceEquals(shot, _activeShot))
            EnterShot(shot, local);

        if (shot.OverrideCameraPose
            && TryResolveShotPose(shot, local, snapshot.CurrentFrame, out CameraShotPose pose))
        {
            _director?.SetSkillShot(shot, pose);
            _skillShotLive = true;
        }
        else if (_skillShotLive)
        {
            _director?.ClearSkillShot(_activeAction.Timeline.CameraSettings.RestoreMode);
            _skillShotLive = false;
        }
    }

    /// <summary>换段时采集 Snapshot Binding，并应用 Hold/Look/Impulse。</summary>
    void EnterShot(CameraShotNotifyState shot, ILocalPlayer local)
    {
        if (_activeShot != null && _activeShot.HoldFollow)
            _manager?.Rig?.ClearFollowHold();
        if (_skillShotLive && !shot.OverrideCameraPose)
        {
            _director?.ClearSkillShot(_activeAction.Timeline.CameraSettings.RestoreMode);
            _skillShotLive = false;
        }

        _activeShot = shot;
        CaptureSnapshotBindings(shot, local);
        if (shot.HoldFollow)
            _manager?.Rig?.SetFollowHold();

        CameraTrackSettings settings = _activeAction.Timeline.CameraSettings;
        if (settings.SuppressLookInput)
            _director?.SetGameplayLookEnabled(false);

        Vector3 direction = local?.PresentationRoot != null
            ? local.PresentationRoot.forward
            : Vector3.forward;
        _feedback?.PlayShotEnter(shot, direction);
    }

    /// <summary>动作结束、打断或 Camera 窗结束时恢复日常镜头。</summary>
    void ClearActiveShot()
    {
        CameraRestoreMode restoreMode = _activeAction != null
            ? _activeAction.Timeline.CameraSettings.RestoreMode
            : CameraRestoreMode.PreviousGameplay;
        ClearShotOnly(restoreMode);
        _activeAction = null;
        _activeInstanceId = 0;
    }

    /// <summary>只清当前镜头段，保留正在播放的 Action 身份。</summary>
    void ClearShotOnly(CameraRestoreMode restoreMode)
    {
        if (_activeShot == null)
            return;

        if (_activeShot.HoldFollow)
            _manager?.Rig?.ClearFollowHold();
        if (_skillShotLive)
            _director?.ClearSkillShot(restoreMode);
        _skillShotLive = false;
        _director?.SetGameplayLookEnabled(true);
        _activeShot = null;
        _hasReferenceSnapshot = false;
        _hasLookAtSnapshot = false;
    }

    /// <summary>进入镜头窗时冻结 Snapshot Binding；Dynamic Binding 留到逐帧解析。</summary>
    void CaptureSnapshotBindings(CameraShotNotifyState shot, ILocalPlayer local)
    {
        _hasReferenceSnapshot = false;
        _hasLookAtSnapshot = false;
        if (shot == null)
            return;

        if (shot.ReferenceBinding?.Space == CameraBindingSpace.Snapshot)
        {
            _hasReferenceSnapshot = TryResolveBindingPose(
                shot.ReferenceBinding,
                local,
                out _referenceSnapshot);
        }

        if (shot.LookAtBinding?.Space == CameraBindingSpace.Snapshot)
        {
            _hasLookAtSnapshot = TryResolveBindingPose(
                shot.LookAtBinding,
                local,
                out _lookAtSnapshot);
        }
    }

    /// <summary>解析当前帧 Dynamic/Snapshot 参考系并调用共享 Spline Pose 求值器。</summary>
    bool TryResolveShotPose(
        CameraShotNotifyState shot,
        ILocalPlayer local,
        int frame,
        out CameraShotPose pose)
    {
        pose = default;
        if (!TryResolveActiveBinding(
                shot.ReferenceBinding,
                local,
                _hasReferenceSnapshot,
                _referenceSnapshot,
                out CameraReferencePose referencePose))
        {
            return false;
        }

        if (!TryResolveActiveBinding(
                shot.LookAtBinding,
                local,
                _hasLookAtSnapshot,
                _lookAtSnapshot,
                out CameraReferencePose lookAtPose))
        {
            return false;
        }

        return CameraShotPoseResolver.TryResolvePose(
            shot,
            referencePose,
            lookAtPose,
            frame,
            out pose);
    }

    /// <summary>Snapshot 只读进入帧缓存；Dynamic 每帧读取当前 Transform。</summary>
    bool TryResolveActiveBinding(
        CameraTransformBinding binding,
        ILocalPlayer local,
        bool hasSnapshot,
        CameraReferencePose snapshot,
        out CameraReferencePose pose)
    {
        if (binding != null && binding.Space == CameraBindingSpace.Snapshot)
        {
            pose = snapshot;
            return hasSnapshot;
        }

        return TryResolveBindingPose(binding, local, out pose);
    }

    /// <summary>从本地角色、当前 SelectedTarget 与可选 AnchorProvider 解析 Binding。</summary>
    static bool TryResolveBindingPose(
        CameraTransformBinding binding,
        ILocalPlayer local,
        out CameraReferencePose pose)
    {
        Transform characterRoot = local?.PresentationRoot != null
            ? local.PresentationRoot
            : local?.Root;
        Transform selectedTarget = ResolveSelectedTarget(local);
        CameraAnchorProvider characterProvider = ResolveAnchorProvider(characterRoot);
        CameraAnchorProvider targetProvider = ResolveAnchorProvider(selectedTarget);
        return CameraShotPoseResolver.TryResolveReferencePose(
            binding,
            characterRoot,
            selectedTarget,
            characterProvider,
            targetProvider,
            out pose);
    }

    /// <summary>从 AimTransform 向上优先查找模型锚点表，再检查其子层级。</summary>
    static CameraAnchorProvider ResolveAnchorProvider(Transform root)
    {
        if (root == null)
            return null;

        CameraAnchorProvider provider = root.GetComponentInParent<CameraAnchorProvider>();
        return provider != null ? provider : root.GetComponentInChildren<CameraAnchorProvider>(true);
    }

    /// <summary>只读唯一 SelectedTarget 的 AimTransform；不触发重新选敌。</summary>
    static Transform ResolveSelectedTarget(ILocalPlayer local)
    {
        if (local?.Actor is not ILocalCameraTargetSource source)
            return null;
        return source.TryGetSelectedTarget(out ITargetable target)
            ? target.AimTransform
            : null;
    }
}

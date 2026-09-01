using UnityEngine;

/// <summary>
/// 权威侧远端玩家入口：有权威 Actor，但不是本机输入/相机拥有者。
/// 必须走 AppControllerBase，以满足 App/Controllers 的架构边界。
/// </summary>
public sealed class RemotePlayerSeat : AppControllerBase, ILocalPlayer
{
    CharacterActor _actor;
    Transform _perceptionRoot;

    /// <summary>原子绑定当前权威 Actor，并把稳定感知锚点重新挂到其槽位逻辑根。</summary>
    public void Bind(CharacterActor actor, Transform simulationRoot)
    {
        if (actor == null)
            throw new System.ArgumentNullException(nameof(actor));
        if (simulationRoot == null)
            throw new System.ArgumentNullException(nameof(simulationRoot));

        _actor = actor;
        if (_perceptionRoot == null)
        {
            var anchor = new GameObject("ActivePartyMemberRoot");
            _perceptionRoot = anchor.transform;
        }

        // LocalPlayerService 缓存此 Transform 引用；切人只换父节点即可让敌人持续追踪当前槽。
        _perceptionRoot.SetParent(simulationRoot, false);
        _perceptionRoot.localPosition = Vector3.zero;
        _perceptionRoot.localRotation = Quaternion.identity;
    }

    /// <inheritdoc />
    public CharacterActor Actor => _actor;

    /// <inheritdoc />
    public Transform Root => _perceptionRoot != null ? _perceptionRoot : transform;

    /// <inheritdoc />
    public Transform PresentationRoot => _actor?.PresentationRoot != null
        ? _actor.PresentationRoot
        : transform;

    /// <inheritdoc />
    public Vector2 LookInput => Vector2.zero;

    /// <inheritdoc />
    public Vector2 MoveInput => Vector2.zero;

    /// <inheritdoc />
    public bool CameraLockPressedThisFrame => false;

    /// <inheritdoc />
    public bool IsLocalPredicted => false;

    /// <inheritdoc />
    public InputManager Input => _actor?.Input;

    /// <inheritdoc />
    public bool HasMoveIntent => _actor?.Input != null && _actor.Input.HasMoveIntent;

    /// <inheritdoc />
    public bool IsPresentingAction =>
        _actor != null && _actor.CurrentState != CharacterStateType.Locomotion;

    /// <summary>远端输入来自网络，忽略本机相机 yaw。</summary>
    public void StageMoveReferenceYaw(float yawDegrees)
    {
    }
}

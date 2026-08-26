using UnityEngine;

/// <summary>
/// 场上一名玩家的只读入口。拥有输入与相机的为本机 Local；花名册也可登记远端玩家。
/// Domain 只依赖本接口，不查场景、不访问 Architecture。
/// </summary>
public interface ILocalPlayer
{
    /// <summary>已装配的角色 Actor；本机座位为 Autonomous，权威 Guest 为 Authority。</summary>
    CharacterActor Actor { get; }

    /// <summary>权威根（逻辑 Transform）。</summary>
    Transform Root { get; }

    /// <summary>相机与表现跟随的插值锚点。</summary>
    Transform PresentationRoot { get; }

    /// <summary>本地渲染帧 Look；远端实现应返回零。</summary>
    Vector2 LookInput { get; }

    /// <summary>本地渲染帧相机相对移动轴；远端实现应返回零。供 L-DIR5 判断后退，不进 InputFrame。</summary>
    Vector2 MoveInput { get; }

    /// <summary>本渲染帧是否按下纯表现 CameraLock。</summary>
    bool CameraLockPressedThisFrame { get; }

    /// <summary>
    /// 本机是否对移动/出招做客户端预测。
    /// Listen / Client 本机座位为 true；权威 RemotePlayerSeat 为 false。
    /// </summary>
    bool IsLocalPredicted { get; }

    /// <summary>量化输入中枢；两端都有 Actor 后非空。</summary>
    InputManager Input { get; }

    /// <summary>本机当前是否有移动输入；客机读设备采样，不得依赖空的 Input。</summary>
    bool HasMoveIntent { get; }

    /// <summary>本机正在播招/受击/死亡；相机跟朝向应暂停，避免连闪甩镜头。</summary>
    bool IsPresentingAction { get; }

    /// <summary>由相机暂存 Orbit yaw，供下一逻辑帧写入 InputFrame。</summary>
    void StageMoveReferenceYaw(float yawDegrees);
}

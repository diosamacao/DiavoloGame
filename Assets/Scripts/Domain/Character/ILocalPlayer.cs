using UnityEngine;

/// <summary>
/// 场上一名玩家的只读入口。拥有输入与相机的为本机 Local；花名册也可登记远端玩家。
/// Domain 只依赖本接口，不查场景、不访问 Architecture。
/// </summary>
public interface ILocalPlayer
{
    /// <summary>已装配的角色 Actor；未就绪时为空。</summary>
    CharacterActor Actor { get; }

    /// <summary>权威根（逻辑 Transform）。</summary>
    Transform Root { get; }

    /// <summary>相机与表现跟随的插值锚点。</summary>
    Transform PresentationRoot { get; }

    /// <summary>本地渲染帧 Look；远端实现应返回零。</summary>
    Vector2 LookInput { get; }

    /// <summary>本渲染帧是否按下纯表现 CameraLock。</summary>
    bool CameraLockPressedThisFrame { get; }

    /// <summary>
    /// 本机是否对移动/出招做客户端预测。
    /// Listen Host 本地玩家恒为 false；远端客机为 true。
    /// </summary>
    bool IsLocalPredicted { get; }

    /// <summary>量化输入中枢；未装配时为空。</summary>
    InputManager Input { get; }

    /// <summary>由相机暂存 Orbit yaw，供下一逻辑帧写入 InputFrame。</summary>
    void StageMoveReferenceYaw(float yawDegrees);
}

using UnityEngine;

/// <summary>朝向调试箭头的只读采样；本机 Actor 与 RemoteProxy 共用，禁止 Visualizer 绑死 PlayerController。</summary>
public interface ICharacterFacingDebugTarget
{
    /// <summary>目标已装配且可采样脚底 / 朝向。</summary>
    bool HasFacingDebugPose { get; }

    /// <summary>表现插值后的脚底世界坐标。</summary>
    Vector3 FacingDebugFeetWorld { get; }

    /// <summary>与该表现帧配对的水平 wish；无输入时为零。</summary>
    Vector3 FacingDebugWishWorld { get; }

    /// <summary>模型水平前向（优先 VisualMotionRoot）。</summary>
    Vector3 FacingDebugModelForward { get; }
}

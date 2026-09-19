using UnityEngine;

/// <summary>只读解释角色模拟结果的表现端口；实现不得回写任何模拟状态。</summary>
public interface ICharacterPresentationSink
{
    /// <summary>供相机和其他表现系统跟随的锚点。</summary>
    Transform PresentationRoot { get; }

    /// <summary>模型视觉残差根；Headless 可为空。</summary>
    Transform VisualMotionRoot { get; }

    /// <summary>当前渲染帧表现位置。</summary>
    Vector3 RenderedPosition { get; }

    /// <summary>逻辑步前释放表现锚点对模拟根的影响。</summary>
    void BeginSimulationStep();

    /// <summary>逻辑步后捕获最新模拟根 Pose。</summary>
    void EndSimulationStep();

    /// <summary>纠偏或传送后立即吸附表现端点。</summary>
    void SnapToSimulationRoot();

    /// <summary>同帧末校正后刷新当前端点而不推进前端点。</summary>
    void RefreshCurrentPoseFromSimulationRoot();

    /// <summary>固定帧前恢复视觉残差的逻辑局部 Pose。</summary>
    void ApplyLogicLocalPose();

    /// <summary>按模拟输出记录冲刺倾身。</summary>
    void SetLeanRollDegrees(float rollDegrees);

    /// <summary>按动作整数帧记录视觉根残差。</summary>
    void CaptureActionFrame(ActionDefinition action, int frame);

    /// <summary>结束动作视觉残差。</summary>
    void EndAction(VisualResidualExitPolicy exitPolicy);

    /// <summary>按渲染插值更新表现根和视觉残差。</summary>
    void Render(float interpolationAlpha, float deltaTimeSeconds);
}

using UnityEngine;

/// <summary>Headless 角色表现空实现；只暴露既有模拟根，不创建额外 GameObject。</summary>
public sealed class NullCharacterPresentationSink : ICharacterPresentationSink
{
    readonly Transform _simulationRoot;

    /// <summary>绑定既有模拟根作为只读跟随目标。</summary>
    public NullCharacterPresentationSink(Transform simulationRoot) =>
        _simulationRoot = simulationRoot;

    /// <inheritdoc />
    public Transform PresentationRoot => _simulationRoot;

    /// <inheritdoc />
    public Transform VisualMotionRoot => null;

    /// <inheritdoc />
    public Vector3 RenderedPosition =>
        _simulationRoot != null ? _simulationRoot.position : Vector3.zero;

    /// <inheritdoc />
    public void BeginSimulationStep() { }

    /// <inheritdoc />
    public void EndSimulationStep() { }

    /// <inheritdoc />
    public void SnapToSimulationRoot() { }

    /// <inheritdoc />
    public void RefreshCurrentPoseFromSimulationRoot() { }

    /// <inheritdoc />
    public void ApplyLogicLocalPose() { }

    /// <inheritdoc />
    public void SetLeanRollDegrees(float rollDegrees) { }

    /// <inheritdoc />
    public void CaptureActionFrame(ActionDefinition action, int frame) { }

    /// <inheritdoc />
    public void EndAction(VisualResidualExitPolicy exitPolicy) { }

    /// <inheritdoc />
    public void Render(float interpolationAlpha, float deltaTimeSeconds) { }
}

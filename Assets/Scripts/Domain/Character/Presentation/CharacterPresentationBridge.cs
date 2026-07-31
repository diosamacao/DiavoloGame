using UnityEngine;

/// <summary>在权威角色根与模型表现锚点之间插值固定帧 Pose，避免模型随逻辑帧阶梯跳动。</summary>
public sealed class CharacterPresentationBridge
{
    const float TeleportSnapDistanceSq = 9f;

    readonly Transform _simulationRoot;
    readonly Transform _presentationRoot;
    Vector3 _previousPosition;
    Vector3 _currentPosition;
    Quaternion _previousRotation;
    Quaternion _currentRotation;

    /// <summary>供相机和其他表现系统跟随的非权威插值锚点。</summary>
    public Transform PresentationRoot => _presentationRoot;

    /// <summary>当前渲染帧表现位置。</summary>
    public Vector3 RenderedPosition => _presentationRoot != null
        ? _presentationRoot.position
        : _currentPosition;

    /// <summary>绑定权威根与运行时表现锚点，并以当前 Pose 初始化双快照。</summary>
    public CharacterPresentationBridge(Transform simulationRoot, Transform presentationRoot)
    {
        _simulationRoot = simulationRoot;
        _presentationRoot = presentationRoot;
        _previousPosition = _currentPosition = simulationRoot.position;
        _previousRotation = _currentRotation = simulationRoot.rotation;
    }

    /// <summary>逻辑 Step 前恢复表现锚点局部原点，确保骨骼挂点和判定读取权威 Pose。</summary>
    public void BeginSimulationStep()
    {
        _previousPosition = _currentPosition;
        _previousRotation = _currentRotation;
        if (_presentationRoot == null)
            return;

        _presentationRoot.localPosition = Vector3.zero;
        _presentationRoot.localRotation = Quaternion.identity;
    }

    /// <summary>逻辑 Step 后捕获新的权威根 Pose。</summary>
    public void EndSimulationStep()
    {
        _currentPosition = _simulationRoot.position;
        _currentRotation = _simulationRoot.rotation;
    }

    /// <summary>按 accumulator 比例更新模型锚点；传送时直接吸附以避免跨场景扫过。</summary>
    public void Render(float interpolationAlpha)
    {
        if (_presentationRoot == null)
            return;

        float alpha = Mathf.Clamp01(interpolationAlpha);
        bool teleported =
            (_currentPosition - _previousPosition).sqrMagnitude > TeleportSnapDistanceSq;
        Vector3 position = teleported
            ? _currentPosition
            : Vector3.Lerp(_previousPosition, _currentPosition, alpha);
        Quaternion rotation = teleported
            ? _currentRotation
            : Quaternion.Slerp(_previousRotation, _currentRotation, alpha);
        _presentationRoot.SetPositionAndRotation(position, rotation);
    }
}

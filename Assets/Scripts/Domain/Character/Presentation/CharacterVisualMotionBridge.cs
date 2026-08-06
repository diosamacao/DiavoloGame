using UnityEngine;

/// <summary>
/// Wave 2：把动作视觉残差写入 VisualMotionRoot；不写 SimulationRoot / MotorSim。
/// 逻辑步贴帧采样，渲染步在前后残差间插值；打断时短时 BlendToZero。
/// </summary>
public sealed class CharacterVisualMotionBridge
{
    const float DefaultBlendSeconds = 0.12f;

    readonly Transform _visualRoot;
    Vector3 _previousResidualMeters;
    Vector3 _currentResidualMeters;
    bool _hasPose;
    bool _blendingOut;
    float _blendElapsed;
    float _blendDuration = DefaultBlendSeconds;
    Vector3 _blendFromMeters;

    /// <summary>模型所在的视觉残差根。</summary>
    public Transform VisualRoot => _visualRoot;

    /// <summary>绑定 VisualMotionRoot（PresentationRoot 子节点）。</summary>
    public CharacterVisualMotionBridge(Transform visualMotionRoot)
    {
        _visualRoot = visualMotionRoot;
        SnapLocalZero();
    }

    /// <summary>
    /// 逻辑 Step 内：把残差贴到当前动作帧（供挂点/表现与逻辑帧对齐）。
    /// 卡肉时帧不变，残差保持。
    /// </summary>
    public void CaptureSimulationFrame(ActionDefinition action, int actionFrame, bool actionActive)
    {
        if (_visualRoot == null)
            return;

        if (_blendingOut)
            return;

        if (!actionActive || action == null)
        {
            // 无动作时保持当前残差，由 EndAction 决定退出
            return;
        }

        ActionBakedMotion baked = action.BakedMotion;
        if (baked == null || !baked.IsReady)
        {
            SetResidualMeters(Vector3.zero);
            return;
        }

        if (!baked.TryGetVisualResidualMm(actionFrame, out int rx, out int rz))
        {
            SetResidualMeters(Vector3.zero);
            return;
        }

        SetResidualMeters(new Vector3(
            MotionQuantization.MmToMeters(rx),
            0f,
            MotionQuantization.MmToMeters(rz)));
        ApplyLogicLocalPose();
    }

    /// <summary>动作正常结束或被打断时处理残差退出。</summary>
    public void EndAction(VisualResidualExitPolicy exitPolicy)
    {
        if (_visualRoot == null)
            return;

        switch (exitPolicy)
        {
            case VisualResidualExitPolicy.SnapToZero:
            case VisualResidualExitPolicy.RequireZeroAtEnd:
                _blendingOut = false;
                SetResidualMeters(Vector3.zero);
                ApplyLogicLocalPose();
                break;
            case VisualResidualExitPolicy.BlendToZero:
                if (_currentResidualMeters.sqrMagnitude < 0.0000001f)
                {
                    _blendingOut = false;
                    ApplyLogicLocalPose();
                    break;
                }

                _blendingOut = true;
                _blendElapsed = 0f;
                _blendDuration = DefaultBlendSeconds;
                _blendFromMeters = _currentResidualMeters;
                break;
        }
    }

    /// <summary>渲染帧：插值残差；BlendOut 时只动模型局部，不碰逻辑根。</summary>
    public void Render(float interpolationAlpha, float deltaTimeSeconds)
    {
        if (_visualRoot == null)
            return;

        if (_blendingOut)
        {
            _blendElapsed += Mathf.Max(0f, deltaTimeSeconds);
            float t = _blendDuration <= 0f ? 1f : Mathf.Clamp01(_blendElapsed / _blendDuration);
            // SmoothStep 减轻回锚顿挫
            float s = t * t * (3f - 2f * t);
            Vector3 local = Vector3.Lerp(_blendFromMeters, Vector3.zero, s);
            _visualRoot.localPosition = local;
            _visualRoot.localRotation = Quaternion.identity;
            if (t >= 1f)
            {
                _blendingOut = false;
                SetResidualMeters(Vector3.zero);
            }

            return;
        }

        if (!_hasPose)
        {
            ApplyLogicLocalPose();
            return;
        }

        float alpha = Mathf.Clamp01(interpolationAlpha);
        _visualRoot.localPosition = Vector3.Lerp(_previousResidualMeters, _currentResidualMeters, alpha);
        _visualRoot.localRotation = Quaternion.identity;
    }

    /// <summary>逻辑步开始时贴齐当前残差（无插值），避免挂点读到上一渲染帧。</summary>
    public void ApplyLogicLocalPose()
    {
        if (_visualRoot == null)
            return;

        _visualRoot.localPosition = _currentResidualMeters;
        _visualRoot.localRotation = Quaternion.identity;
    }

    void SetResidualMeters(Vector3 meters)
    {
        if (!_hasPose)
        {
            _previousResidualMeters = meters;
            _currentResidualMeters = meters;
            _hasPose = true;
            return;
        }

        _previousResidualMeters = _currentResidualMeters;
        _currentResidualMeters = meters;
    }

    void SnapLocalZero()
    {
        _previousResidualMeters = Vector3.zero;
        _currentResidualMeters = Vector3.zero;
        _hasPose = true;
        _blendingOut = false;
        if (_visualRoot != null)
        {
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
        }
    }
}

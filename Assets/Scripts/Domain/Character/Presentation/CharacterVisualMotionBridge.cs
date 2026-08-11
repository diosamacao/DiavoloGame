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
    float _leanRollDegrees;

    /// <summary>模型所在的视觉残差根。</summary>
    public Transform VisualRoot => _visualRoot;

    /// <summary>当前视觉倾身 Roll（度）；仅改 VisualMotionRoot，不改权威根。</summary>
    public float LeanRollDegrees => _leanRollDegrees;

    /// <summary>是否正在将残差 Blend 回原点（供调试/测试）。</summary>
    public bool IsBlendingOut => _blendingOut;

    /// <summary>绑定 VisualMotionRoot（PresentationRoot 子节点）。</summary>
    public CharacterVisualMotionBridge(Transform visualMotionRoot)
    {
        _visualRoot = visualMotionRoot;
        SnapLocalZero();
    }

    /// <summary>
    /// L-DIR4：写入视觉倾身 Roll。残差位移仍走 localPosition；与残差旋转互斥（残差不写旋转）。
    /// </summary>
    public void SetLeanRollDegrees(float rollDegrees)
    {
        _leanRollDegrees = rollDegrees;
        if (_visualRoot == null || _blendingOut)
            return;

        // 逻辑步可立即贴倾身，避免等 Render 才看到
        _visualRoot.localRotation = Quaternion.Euler(0f, 0f, _leanRollDegrees);
    }

    /// <summary>
    /// 逻辑 Step 内：把残差贴到当前动作帧（供挂点/表现与逻辑帧对齐）。
    /// 卡肉时帧不变，残差保持。回锚期间若新动作起手则取消 Blend 并接管。
    /// </summary>
    public void CaptureSimulationFrame(ActionDefinition action, int actionFrame, bool actionActive)
    {
        if (_visualRoot == null)
            return;

        if (!actionActive || action == null)
        {
            // 无动作时保持当前残差，由 EndAction 决定退出；回锚中也不采样
            return;
        }

        // 连招/再起手：取消未完成的回锚，避免旧 Blend 盖住新帧残差
        if (_blendingOut)
            CancelBlendOut();

        ActionBakedMotion baked = action.BakedMotion;
        if (baked == null || !baked.IsReady)
        {
            SetResidualMeters(Vector3.zero);
            ApplyLogicLocalPose();
            return;
        }

        if (!baked.TryGetVisualResidualMm(actionFrame, out int rx, out int rz))
        {
            SetResidualMeters(Vector3.zero);
            ApplyLogicLocalPose();
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
                CancelBlendOut();
                SetResidualMetersBoth(Vector3.zero);
                ApplyLogicLocalPose();
                break;
            case VisualResidualExitPolicy.BlendToZero:
                if (_currentResidualMeters.sqrMagnitude < 0.0000001f)
                {
                    CancelBlendOut();
                    SetResidualMetersBoth(Vector3.zero);
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
            // 回锚期间仍保留倾身，避免动作结束瞬间直立闪一下
            _visualRoot.localRotation = Quaternion.Euler(0f, 0f, _leanRollDegrees);
            if (t >= 1f)
            {
                // 前后快照一并清零，避免结束后又按 previous→current 插值把残差拽回来
                CancelBlendOut();
                SetResidualMetersBoth(Vector3.zero);
                _visualRoot.localPosition = Vector3.zero;
                _visualRoot.localRotation = Quaternion.Euler(0f, 0f, _leanRollDegrees);
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
        _visualRoot.localRotation = Quaternion.Euler(0f, 0f, _leanRollDegrees);
    }

    /// <summary>
    /// 逻辑步贴齐当前残差（无插值），避免挂点读到上一渲染帧。
    /// BlendToZero 期间禁止回写，否则会每逻辑帧把模型拽回满残差，与 Render 回锚打架形成抖动。
    /// </summary>
    public void ApplyLogicLocalPose()
    {
        if (_visualRoot == null || _blendingOut)
            return;

        _visualRoot.localPosition = _currentResidualMeters;
        _visualRoot.localRotation = Quaternion.Euler(0f, 0f, _leanRollDegrees);
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

    /// <summary>前后残差快照同时写入（回锚结束/硬清零，避免插值回拉）。</summary>
    void SetResidualMetersBoth(Vector3 meters)
    {
        _previousResidualMeters = meters;
        _currentResidualMeters = meters;
        _hasPose = true;
    }

    void CancelBlendOut()
    {
        _blendingOut = false;
        _blendElapsed = 0f;
    }

    void SnapLocalZero()
    {
        CancelBlendOut();
        SetResidualMetersBoth(Vector3.zero);
        _leanRollDegrees = 0f;
        if (_visualRoot != null)
        {
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
        }
    }
}

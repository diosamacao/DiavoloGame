using UnityEngine;

/// <summary>AI 量化输入写入器；不伪装设备，只为指定逻辑帧构造 InputFrame。</summary>
public sealed class AIInputWriter
{
    /// <summary>可合成 Pressed→Released 边沿的按钮集合（由 Intent Profile 探测）。</summary>
    readonly bool[] _canPulse;

    readonly ButtonEdge[] _edges;

    sbyte _moveX;
    sbyte _moveY;
    bool _enabled;

    /// <summary>从意图 Profile 确认哪些按钮可进入统一语义管线。</summary>
    public AIInputWriter(GameplayIntentProfile profile)
    {
        _canPulse = new bool[InputButtonCount];
        _edges = new ButtonEdge[InputButtonCount];
        DetectPulseableButtons(profile, _canPulse);
    }

    /// <summary>EditMode / 工具：直接指定可脉冲按钮，绕过 InputAction 引用。</summary>
    public static AIInputWriter CreateForEditorTests(params InputButton[] pulseableButtons)
    {
        var writer = new AIInputWriter(pulseableButtons);
        return writer;
    }

    AIInputWriter(InputButton[] pulseableButtons)
    {
        _canPulse = new bool[InputButtonCount];
        _edges = new ButtonEdge[InputButtonCount];
        if (pulseableButtons == null)
            return;
        for (int i = 0; i < pulseableButtons.Length; i++)
        {
            int index = ToIndex(pulseableButtons[i]);
            if (index >= 0)
                _canPulse[index] = true;
        }
    }

    static int InputButtonCount => (int)InputButton.Skill + 1;

    /// <summary>是否存在可合成的 Always + Pressed → Attack 映射。</summary>
    public bool CanPulseAttack => CanPulse(InputButton.Attack);

    /// <summary>是否存在可合成的 Always + Pressed → Dodge 映射。</summary>
    public bool CanPulseDodge => CanPulse(InputButton.Dodge);

    /// <summary>指定按钮是否可被 AI 脉冲。</summary>
    public bool CanPulse(InputButton button)
    {
        int index = ToIndex(button);
        return index >= 0 && _canPulse[index];
    }

    /// <summary>量化并保存后续逻辑帧持续使用的移动轴。</summary>
    public void SetMove(Vector2 move)
    {
        Vector2 clamped = Vector2.ClampMagnitude(move, 1f);
        _moveX = InputQuantizer.QuantizeAxis(clamped.x);
        _moveY = InputQuantizer.QuantizeAxis(clamped.y);
    }

    /// <summary>请求下一逻辑帧产生一次 Attack Pressed，随后一帧 Released。</summary>
    public bool PulseAttack() => Pulse(InputButton.Attack);

    /// <summary>请求下一逻辑帧产生一次 Dodge Pressed，随后一帧 Released。</summary>
    public bool PulseDodge() => Pulse(InputButton.Dodge);

    /// <summary>请求指定按钮的单次 Pressed→Released 边沿（同按钮不可叠压未消费脉冲）。</summary>
    public bool Pulse(InputButton button)
    {
        int index = ToIndex(button);
        if (!_enabled || index < 0 || !_canPulse[index])
            return false;
        if (_edges[index].PressPending)
            return false;

        _edges[index].PressPending = true;
        _edges[index].ReleasePending = false;
        return true;
    }

    /// <summary>清空连续移动与待发按钮边沿。</summary>
    public void ClearAll()
    {
        _moveX = 0;
        _moveY = 0;
        for (int i = 0; i < _edges.Length; i++)
        {
            _edges[i].PressPending = false;
            _edges[i].ReleasePending = false;
        }
    }

    /// <summary>为当前 AI 逻辑帧构造完整量化输入；边沿严格只出现一次。</summary>
    public InputFrame BuildFrame(long frameIndex, SimActorId actorId)
    {
        if (!_enabled)
            return InputFrame.Empty(frameIndex, actorId);

        ulong pressed = 0ul;
        ulong held = 0ul;
        ulong released = 0ul;

        for (int i = 0; i < _edges.Length; i++)
        {
            ulong mask = InputButtonMask.Of((InputButton)i);
            if (_edges[i].PressPending)
            {
                pressed |= mask;
                held |= mask;
                _edges[i].PressPending = false;
                _edges[i].ReleasePending = true;
            }
            else if (_edges[i].ReleasePending)
            {
                released |= mask;
                _edges[i].ReleasePending = false;
            }
        }

        return new InputFrame(
            frameIndex,
            actorId,
            _moveX,
            _moveY,
            pressed,
            held,
            released);
    }

    /// <summary>启用 AI 输入帧输出。</summary>
    public void Enable() => _enabled = true;

    /// <summary>禁用并清空 AI 输入状态。</summary>
    public void Disable()
    {
        _enabled = false;
        ClearAll();
    }

    /// <summary>按 Profile 探测 Always+Pressed 且意图与按钮匹配的可脉冲集合。</summary>
    static void DetectPulseableButtons(GameplayIntentProfile profile, bool[] canPulse)
    {
        if (profile == null || canPulse == null)
            return;

        for (int i = 0; i < profile.Bindings.Count; i++)
        {
            GameplayIntentBinding binding = profile.Bindings[i];
            if (!binding.IsValid
                || binding.Phase != GameplayIntentInputPhase.Pressed
                || binding.Condition != GameplayIntentCondition.Always)
            {
                continue;
            }

            if (TryMapPulseable(binding.Button, binding.Intent, out InputButton button))
            {
                int index = ToIndex(button);
                if (index >= 0)
                    canPulse[index] = true;
            }
        }
    }

    /// <summary>Intent 与稳定 Button 对齐时才允许 AI 合成该边沿。</summary>
    static bool TryMapPulseable(InputButton button, GameplayIntentType intent, out InputButton pulseButton)
    {
        pulseButton = button;
        switch (button)
        {
            case InputButton.Attack:
                return intent == GameplayIntentType.Attack;
            case InputButton.Dodge:
                return intent == GameplayIntentType.Dodge;
            case InputButton.HeavyAttack:
                // Heavy 若未单独 Intent，允许与 Attack 族或未来扩展；当前以按钮位为准且 Intent≠None
                return intent != GameplayIntentType.None;
            case InputButton.Skill:
                return intent == GameplayIntentType.Special || intent == GameplayIntentType.Ultimate;
            default:
                return false;
        }
    }

    static int ToIndex(InputButton button)
    {
        int index = (int)button;
        return index >= 0 && index < InputButtonCount ? index : -1;
    }

    struct ButtonEdge
    {
        public bool PressPending;
        public bool ReleasePending;
    }
}

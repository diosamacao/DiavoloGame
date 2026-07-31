using UnityEngine;

/// <summary>AI 量化输入写入器；不伪装设备，只为指定逻辑帧构造 InputFrame。</summary>
public sealed class AIInputWriter
{
    readonly bool _canPulseAttack;
    sbyte _moveX;
    sbyte _moveY;
    bool _enabled;
    bool _attackPressedPending;
    bool _attackReleasePending;

    /// <summary>从意图 Profile 确认 Attack 按钮会进入统一语义管线。</summary>
    public AIInputWriter(GameplayIntentProfile profile)
    {
        _canPulseAttack = HasAttackBinding(profile);
    }

    /// <summary>是否存在可合成的 Always + Pressed → Attack 映射。</summary>
    public bool CanPulseAttack => _canPulseAttack;

    /// <summary>量化并保存后续逻辑帧持续使用的移动轴。</summary>
    public void SetMove(Vector2 move)
    {
        Vector2 clamped = Vector2.ClampMagnitude(move, 1f);
        _moveX = InputQuantizer.QuantizeAxis(clamped.x);
        _moveY = InputQuantizer.QuantizeAxis(clamped.y);
    }

    /// <summary>请求下一逻辑帧产生一次 Attack Pressed，随后一帧产生 Released。</summary>
    public bool PulseAttack()
    {
        if (!_enabled || !CanPulseAttack || _attackPressedPending)
            return false;

        _attackPressedPending = true;
        _attackReleasePending = false;
        return true;
    }

    /// <summary>清空连续移动与待发按钮边沿。</summary>
    public void ClearAll()
    {
        _moveX = 0;
        _moveY = 0;
        _attackPressedPending = false;
        _attackReleasePending = false;
    }

    /// <summary>为当前 AI 逻辑帧构造完整量化输入；边沿严格只出现一次。</summary>
    public InputFrame BuildFrame(long frameIndex, SimActorId actorId)
    {
        if (!_enabled)
            return InputFrame.Empty(frameIndex, actorId);

        ulong pressed = 0ul;
        ulong held = 0ul;
        ulong released = 0ul;
        ulong attackMask = InputButtonMask.Of(InputButton.Attack);

        if (_attackPressedPending)
        {
            pressed = attackMask;
            held = attackMask;
            _attackPressedPending = false;
            _attackReleasePending = true;
        }
        else if (_attackReleasePending)
        {
            released = attackMask;
            _attackReleasePending = false;
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

    /// <summary>检查 Profile 是否通过稳定 Attack bit 提供基础起手规则。</summary>
    static bool HasAttackBinding(GameplayIntentProfile profile)
    {
        if (profile == null)
            return false;

        for (int i = 0; i < profile.Bindings.Count; i++)
        {
            GameplayIntentBinding binding = profile.Bindings[i];
            if (binding.IsValid
                && binding.Button == InputButton.Attack
                && binding.Intent == GameplayIntentType.Attack
                && binding.Phase == GameplayIntentInputPhase.Pressed
                && binding.Condition == GameplayIntentCondition.Always)
            {
                return true;
            }
        }

        return false;
    }
}

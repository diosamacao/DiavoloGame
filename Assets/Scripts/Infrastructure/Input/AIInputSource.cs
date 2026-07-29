using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>AI 输入源：生成与 GameplayIntentProfile 物理 id 对齐的移动和攻击帧。</summary>
public sealed class AIInputSource : ICharacterInputSource
{
    readonly string _attackInputId;
    Vector2 _move;
    bool _enabled;
    bool _attackPressedPending;
    bool _attackReleasePending;

    /// <summary>从现有意图 Profile 解析 Attack 的 Pressed 物理 id，避免新增旁路意图管线。</summary>
    public AIInputSource(GameplayIntentProfile profile)
    {
        _attackInputId = ResolveAttackInputId(profile);
    }

    /// <summary>是否找到了可合成 Attack 的配置映射。</summary>
    public bool CanPulseAttack => !string.IsNullOrEmpty(_attackInputId);

    /// <summary>设置下一次采样持续输出的移动轴。</summary>
    public void SetMove(Vector2 move)
    {
        _move = Vector2.ClampMagnitude(move, 1f);
    }

    /// <summary>请求下一帧产生一次 Attack Pressed，随后自动产生 Released。</summary>
    public bool PulseAttack()
    {
        if (!_enabled || !CanPulseAttack || _attackPressedPending)
            return false;

        _attackPressedPending = true;
        _attackReleasePending = false;
        return true;
    }

    /// <summary>清空移动和离散输入；用于受击、死亡与禁用。</summary>
    public void ClearAll()
    {
        _move = Vector2.zero;
        _attackPressedPending = false;
        _attackReleasePending = false;
    }

    /// <summary>采样一帧 synthetic 输入；攻击脉冲严格只占一个 Pressed 帧。</summary>
    public PlayerInputFrame CaptureFrame()
    {
        if (!_enabled)
            return PlayerInputFrame.Empty;

        string[] pressed = Array.Empty<string>();
        string[] held = Array.Empty<string>();
        string[] released = Array.Empty<string>();

        if (_attackPressedPending)
        {
            pressed = new[] { _attackInputId };
            held = new[] { _attackInputId };
            _attackPressedPending = false;
            _attackReleasePending = true;
        }
        else if (_attackReleasePending)
        {
            released = new[] { _attackInputId };
            _attackReleasePending = false;
        }

        return new PlayerInputFrame(_move, Vector2.zero, pressed, held, released);
    }

    /// <summary>AI 已从 GameplayIntentProfile 解析所需 id，无需设备引用配置。</summary>
    public void ConfigureDiscreteInputs(InputActionReference[] references) { }

    /// <summary>启用 AI 帧输出。</summary>
    public void Enable() => _enabled = true;

    /// <summary>禁用并清空 AI 帧输出。</summary>
    public void Disable()
    {
        _enabled = false;
        ClearAll();
    }

    /// <summary>选择第一个始终生效的 Attack Pressed 绑定作为 AI 合成键。</summary>
    static string ResolveAttackInputId(GameplayIntentProfile profile)
    {
        if (profile == null)
            return string.Empty;

        for (int i = 0; i < profile.Bindings.Count; i++)
        {
            GameplayIntentBinding binding = profile.Bindings[i];
            if (binding.IsValid
                && binding.Intent == GameplayIntentType.Attack
                && binding.Phase == GameplayIntentInputPhase.Pressed
                && binding.Condition == GameplayIntentCondition.Always)
            {
                return binding.InputId;
            }
        }

        return string.Empty;
    }
}

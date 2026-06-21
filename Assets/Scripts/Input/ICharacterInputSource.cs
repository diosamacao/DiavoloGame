using UnityEngine.InputSystem;

/// <summary>角色输入源抽象：本地设备、AI、回放、网络均可实现并喂给 InputManager。</summary>
public interface ICharacterInputSource
{
    /// <summary>采集一帧输入快照。</summary>
    PlayerInputFrame CaptureFrame();

    /// <summary>配置离散输入引用；AI 输入源可忽略该调用。</summary>
    void ConfigureDiscreteInputs(InputActionReference[] references);

    /// <summary>启用输入源。</summary>
    void Enable();

    /// <summary>禁用输入源。</summary>
    void Disable();
}


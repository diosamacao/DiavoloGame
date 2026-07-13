/// <summary>动作输入触发类型；Pressed 已落地，Held / Released 枚举预留供 Trigger 与后续缓冲扩展。</summary>
public enum ActionInputTrigger
{
    /// <summary>按下瞬间触发（对应 InputReader 的 WasPressedThisFrame / 输入缓冲）。</summary>
    Pressed = 0,

    /// <summary>按住触发（长按攻击/闪避等）；缓冲与匹配逻辑待后续接入。</summary>
    Held = 1,

    /// <summary>松开触发；缓冲与匹配逻辑待后续接入。</summary>
    Released = 2,
}

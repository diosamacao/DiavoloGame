/// <summary>一次动作解析请求：描述"输入侧"意图（哪个离散输入、何种触发类型）。</summary>
public readonly struct ActionRequest
{
    /// <summary>构造动作解析请求；trigger 默认 Pressed。</summary>
    public ActionRequest(string inputId, ActionInputTrigger trigger = ActionInputTrigger.Pressed)
    {
        InputId = inputId;
        Trigger = trigger;
    }

    /// <summary>离散输入 id（= InputAction 名）。</summary>
    public string InputId { get; }

    /// <summary>输入触发类型。</summary>
    public ActionInputTrigger Trigger { get; }

    /// <summary>是否为有效请求（输入 id 非空）。</summary>
    public bool IsValid => !string.IsNullOrEmpty(InputId);
}

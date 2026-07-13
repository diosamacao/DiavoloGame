using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 招式触发条件：由何种离散输入、何种触发类型进入本招。
/// 配置在 ActionDefinition 上；Cancel 匹配与图边路由读取目标招的 Trigger，不再写在 CancelWindow / 边上。
/// </summary>
[Serializable]
public class ActionTrigger
{
    [Tooltip("触发本招的 Input System Action（运行时 id = Action 名）。")]
    [SerializeField] InputActionReference input;

    [Tooltip("Pressed / Held / Released；当前运行时主路径为 Pressed。")]
    [SerializeField] ActionInputTrigger kind = ActionInputTrigger.Pressed;

    /// <summary>离散输入 id（InputAction 名）；无效绑定时为 null。</summary>
    public string InputId => InputBindingUtils.GetInputId(input);

    /// <summary>原始 InputActionReference，供出招表收集输入注册列表。</summary>
    public InputActionReference InputReference => input;

    /// <summary>触发类型（按下 / 长按 / 松开）。</summary>
    public ActionInputTrigger Kind => kind;

    /// <summary>输入引用有效且 kind 可用时视为已配置。</summary>
    public bool IsValid => InputBindingUtils.IsValid(input);

    /// <summary>与一次解析请求是否匹配（inputId + kind）。</summary>
    public bool Matches(in ActionRequest request) =>
        IsValid
        && request.IsValid
        && string.Equals(InputId, request.InputId, StringComparison.Ordinal)
        && kind == request.Trigger;

    /// <summary>编辑器与调试用短标签，如 Attack● / Dodge●Held。</summary>
    public string DisplayLabel
    {
        get
        {
            if (!IsValid)
                return "(未配置)";

            string suffix = kind == ActionInputTrigger.Pressed ? "●" : "●" + kind;
            return InputId + suffix;
        }
    }
}

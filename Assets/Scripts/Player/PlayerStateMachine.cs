using UnityEngine;

/// <summary>玩家状态机宿主；Motor 快照由 PlayerController 单向 Push，不引用 PlayerController。</summary>
public class PlayerStateMachine : CharacterStateMachine
{
    protected override void ConfigureContext(CharacterContext context)
    {
        context.ActionRuntime = GetComponent<ActionRuntimeController>();
    }

    /// <summary>Motor 数据已由 PlayerController PushMotorSnapshot 写入，无需再拉 PlayerController。</summary>
    protected override void UpdateContext() { }
}

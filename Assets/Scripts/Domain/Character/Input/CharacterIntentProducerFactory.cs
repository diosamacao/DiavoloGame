using System;

/// <summary>在 Character 边界内组装 GameplayIntentProducer，隐藏角色状态条件实现。</summary>
public static class CharacterIntentProducerFactory
{
    /// <summary>用既有角色模拟组件创建意图生产器；仅返回 Input 层公开端口。</summary>
    public static GameplayIntentProducer Create(
        GameplayIntentProfile profile,
        InputManager input,
        GameplayIntentBuffer intentBuffer,
        ICharacterStateMachine stateMachine,
        LocomotionStateMachine locomotion,
        ActionSim actionSim,
        Func<bool> hasPerfectDodgeCounter,
        Func<bool> hasAssistFollowUp)
    {
        var context = new CharacterGameplayIntentContext(
            stateMachine,
            locomotion,
            actionSim,
            hasPerfectDodgeCounter);
        return new GameplayIntentProducer(
            profile,
            input,
            intentBuffer,
            context.Matches,
            hasPerfectDodgeCounter,
            hasAssistFollowUp);
    }
}

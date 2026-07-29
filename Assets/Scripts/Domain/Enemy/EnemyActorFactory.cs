using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>敌人实例工厂；在共享 CharacterActor 管线上装配 AI、生命值和 Hurtbox。</summary>
public static class EnemyActorFactory
{
    /// <summary>按 EnemyDefinition 创建完整敌人句柄；架构注册仍由 App Controller 负责。</summary>
    public static EnemyHandle Create(
        GameObject owner,
        Transform root,
        EnemyDefinition definition,
        Func<Transform> targetProvider,
        Func<IReadOnlyList<IHurtboxTarget>> activeTargetsProvider,
        Action<ActionHitContext, IHurtboxTarget, IActionHitReceiver, Transform> hitDetected,
        CharacterReactionResolver reactionResolver)
    {
        CharacterConfig config = definition.CharacterConfig;
        var input = new AIInputSource(config.GameplayIntentProfile);
        if (!input.CanPulseAttack)
        {
            Debug.LogWarning(
                "EnemyActorFactory: GameplayIntentProfile 缺少 Always + Pressed → Attack 映射，敌人只能追击。",
                owner);
        }

        var facingProxyObject = new GameObject($"{definition.DisplayName}_FacingProxy");
        Transform facingProxy = facingProxyObject.transform;
        facingProxy.position = root.position;
        facingProxy.rotation = root.rotation;

        CharacterActor actor = CharacterActorFactory.Create(
            owner,
            root,
            config,
            definition.TeamId,
            input,
            facingProxy,
            activeTargetsProvider,
            hitDetected,
            out ActionExecutor actionExecutor,
            out CharacterAnimationService animation);

        var health = new EnemyHealth(definition.MaxHp);
        var perception = new EnemyPerception(
            root,
            targetProvider,
            () => actor.CurrentState,
            health);
        var brain = new EnemyBrain(definition.BrainProfile, perception, input, facingProxy);
        var reactionService = new CharacterReactionService(
            health,
            actor,
            reactionResolver,
            _ => brain.NotifyHit(),
            (_, _) => brain.NotifyDeath());
        var target = new CharacterHurtboxTarget(
            root,
            root,
            definition.TeamId,
            config.Combat.Hurtbox,
            health);

        return new EnemyHandle(
            definition,
            root,
            actor,
            actionExecutor,
            animation,
            brain,
            health,
            target,
            facingProxy,
            reactionService);
    }
}

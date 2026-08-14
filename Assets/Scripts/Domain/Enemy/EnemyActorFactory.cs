using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>敌人实例工厂；在共享 CharacterActor 管线上装配 AI、Vitality 和 Hurtbox。</summary>
public static class EnemyActorFactory
{
    /// <summary>按 EnemyDefinition 创建完整敌人句柄；playerRootsProvider 提供感知候选，架构注册仍由 App Controller 负责。</summary>
    public static EnemyHandle Create(
        GameObject owner,
        Transform root,
        EnemyDefinition definition,
        Func<IReadOnlyList<Transform>> playerRootsProvider,
        Func<IReadOnlyList<IHurtboxTarget>> activeTargetsProvider,
        CombatHitPipeline combatHitPipeline,
        CharacterReactionResolver reactionResolver,
        ISimCollisionWorld collisionWorld = null)
    {
        CharacterConfig config = definition.CharacterConfig;
        var actionEntryRequests = new ActionEntryRequestBuffer();
        var locomotionDesires = new LocomotionDesireBuffer();
        // CharacterActor 装配仍依赖全局 Intent Profile（玩家同管线）；敌人出招不经 Intent
        if (GameplayIntentSettings.Active == null)
            throw new InvalidOperationException(
                "EnemyActorFactory: 全局 GameplayIntentProfile 未就绪。");

        var facingProxyObject = new GameObject($"{definition.DisplayName}_FacingProxy");
        Transform facingProxy = facingProxyObject.transform;
        facingProxy.position = root.position;
        facingProxy.rotation = root.rotation;

        CharacterActor actor = CharacterActorFactory.Create(
            owner,
            root,
            config,
            definition.TeamId,
            null,
            activeTargetsProvider,
            combatHitPipeline,
            out ActionSim actionSim,
            out CharacterAnimationService animation,
            collisionWorld,
            locomotionDesires,
            actionEntryRequests);

        // 敌人 MaxHp 以 Definition 为准，覆盖 Config 默认
        actor.Vitality.ResetMaxHealthPoints(Mathf.RoundToInt(definition.MaxHp));

        var perception = new EnemyPerception(
            root,
            playerRootsProvider,
            () => actor.CurrentState,
            () => actor.Vitality.IsDead);

        // 经 IEnemyBehaviorTreeAsset 取 Runner；Brain 不持有具体树类型
        IEnemyBehaviorRunner behaviorRunner = null;
        IEnemyPathQuery pathQuery = new StraightPathQuery();
        EnemyBrainProfile brainProfile = definition.BrainProfile;
        if (brainProfile != null && brainProfile.EnableCombatActions)
        {
            IEnemyBehaviorTreeAsset treeAsset = definition.BehaviorTree;
            if (treeAsset == null)
            {
                throw new InvalidOperationException(
                    $"EnemyActorFactory: {definition.DisplayName} 开启 Combat Actions 但 BehaviorTree 为空。");
            }

            var buildContext = new EnemyBehaviorBuildContext(brainProfile, pathQuery);
            behaviorRunner = treeAsset.CreateRunner(in buildContext);
        }

        var brain = new EnemyBrain(
            brainProfile,
            perception,
            facingProxy,
            behaviorRunner,
            pathQuery,
            actionEntryRequests,
            locomotionDesires);
        var reactionService = new CharacterReactionService(
            actor.Vitality,
            actor,
            reactionResolver,
            _ => brain.NotifyHit(),
            (_, _) => brain.NotifyDeath());
        var target = new CharacterHurtboxTarget(
            root,
            root,
            definition.TeamId,
            config.Combat.Hurtbox,
            actor.Vitality,
            actionSim,
            () => actor.SimulationId,
            actor.MotorSim);

        return new EnemyHandle(
            definition,
            root,
            actor,
            actionSim,
            animation,
            brain,
            target,
            facingProxy,
            reactionService);
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>战斗角色注册条目，集中暴露角色实例和动画门面。</summary>
public readonly struct CombatActorEntry
{
    /// <summary>创建战斗角色注册条目。</summary>
    public CombatActorEntry(
        CharacterActor actor,
        CharacterAnimationService animation)
    {
        Actor = actor;
        Animation = animation;
    }

    /// <summary>单角色运行实例。</summary>
    public CharacterActor Actor { get; }

    /// <summary>动画门面；卡肉通过 SetSpeed 冻结，不直写 Animator。</summary>
    public CharacterAnimationService Animation { get; }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>CooldownGate 定义；子节点 Success 时写入冷却（作者填秒，Build 转帧）。</summary>
[Serializable]
public sealed class CooldownGateNodeDef : EnemyBehaviorNodeDef
{
    [SerializeField] string cooldownId = EnemyCooldownIds.Dodge;
    [SerializeField] float cooldownSeconds = 1f;
    [SerializeReference] public EnemyBehaviorNodeDef child;

    /// <summary>冷却 id（Graph Inspector 可编）。</summary>
    public string CooldownId
    {
        get => cooldownId;
        set => cooldownId = value;
    }

    /// <summary>子节点 Success 后的冷却秒数；CombatRequest 会先暂存至 Brain 起手确认。</summary>
    public float CooldownSeconds
    {
        get => cooldownSeconds;
        set => cooldownSeconds = Mathf.Max(0f, value);
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new CooldownGateNode(
            cooldownId,
            EnemyBehaviorTime.SecondsToFrames(cooldownSeconds),
            child != null ? child.Build() : new StopMoveAction()));
}

using UnityEngine;

/// <summary>可被 Hitbox 命中的目标接口。</summary>
public interface IHurtboxTarget
{
    /// <summary>用于命中排序与去重的稳定模拟身份；无效目标不进入权威结算。</summary>
    SimActorId SimulationId { get; }

    /// <summary>受击目标根节点，用于排除自身并生成命中方向。</summary>
    Transform TargetTransform { get; }

    /// <summary>当前帧的世界空间受击 OBB。</summary>
    HitboxOrientedBox GetWorldHurtbox();

    /// <summary>被命中时回调。</summary>
    void OnHit(in ActionHitContext context);
}

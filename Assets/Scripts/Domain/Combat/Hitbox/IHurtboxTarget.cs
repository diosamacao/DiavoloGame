using UnityEngine;

/// <summary>可被 Hitbox 命中的目标接口。</summary>
public interface IHurtboxTarget
{
    /// <summary>用于命中排序与去重的稳定模拟身份；无效目标不进入权威结算。</summary>
    SimActorId SimulationId { get; }

    /// <summary>受击目标根节点，用于排除自身并生成命中方向。</summary>
    Transform TargetTransform { get; }

    /// <summary>逻辑坐标受击 OBB（MotorSim 根位姿）；运行时命中权威入口。</summary>
    HitboxOrientedBox GetLogicalHurtbox();

    /// <summary>逻辑根 Pose（位置+朝向）；吸附/重定位用，不读表现骨骼。</summary>
    SimCombatPose GetLogicalCombatPose();

    /// <summary>被命中时回调。</summary>
    void OnHit(in ActionHitContext context);
}

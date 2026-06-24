using UnityEngine;

/// <summary>可被索敌系统选中的目标；在 IHurtboxTarget 基础上提供瞄准点、存活与阵营信息。</summary>
public interface ITargetable : IHurtboxTarget
{
    /// <summary>索敌朝向用的世界空间瞄准点。</summary>
    Transform AimTransform { get; }

    /// <summary>目标是否仍可被选中。</summary>
    bool IsAlive { get; }

    /// <summary>当前生命值；无血量系统时可返回 float.MaxValue。</summary>
    float CurrentHealth { get; }

    /// <summary>阵营 id；与攻击者不同阵营才可被选中。</summary>
    int TeamId { get; }
}

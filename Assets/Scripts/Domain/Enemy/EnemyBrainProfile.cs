using UnityEngine;

/// <summary>敌人追击、攻击与受控时序配置。</summary>
[CreateAssetMenu(fileName = "EnemyBrainProfile", menuName = "ACT/Enemy/Brain Profile")]
public sealed class EnemyBrainProfile : ScriptableObject
{
    [SerializeField] float aggroRadius = 10f;
    [SerializeField] float loseAggroRadius = 14f;
    [SerializeField] float attackRange = 2f;
    [SerializeField] float attackCooldownSeconds = 1.2f;
    [SerializeField, Range(0f, 1f)] float chaseMoveMagnitude = 1f;
    [SerializeField] float stopDistance = 1.2f;
    [SerializeField] float repathIntervalSeconds = 0.1f;
    [SerializeField] bool faceTargetWhileChase = true;
    [SerializeField] float failedAttackRetrySeconds = 0.2f;
    [SerializeField] float deathDespawnDelaySeconds = 0.5f;

    /// <summary>进入仇恨的水平距离。</summary>
    public float AggroRadius => Mathf.Max(0f, aggroRadius);
    /// <summary>脱离仇恨的水平距离，至少等于进战半径。</summary>
    public float LoseAggroRadius => Mathf.Max(AggroRadius, loseAggroRadius);
    /// <summary>允许请求攻击的水平距离。</summary>
    public float AttackRange => Mathf.Max(0f, attackRange);
    /// <summary>一次成功攻击结束后的冷却时间。</summary>
    public float AttackCooldownSeconds => Mathf.Max(0f, attackCooldownSeconds);
    /// <summary>追击时写入移动轴的幅度。</summary>
    public float ChaseMoveMagnitude => Mathf.Clamp01(chaseMoveMagnitude);
    /// <summary>贴近目标后停止移动的距离。</summary>
    public float StopDistance => Mathf.Max(0f, stopDistance);
    /// <summary>刷新假相机朝向的最小间隔。</summary>
    public float RepathIntervalSeconds => Mathf.Max(0f, repathIntervalSeconds);
    /// <summary>追击时是否刷新面向目标的假相机。</summary>
    public bool FaceTargetWhileChase => faceTargetWhileChase;
    /// <summary>攻击起手失败后的防抖时间。</summary>
    public float FailedAttackRetrySeconds => Mathf.Max(0.02f, failedAttackRetrySeconds);
    /// <summary>死亡表现完成后的额外回收等待。</summary>
    public float DeathDespawnDelaySeconds => Mathf.Max(0f, deathDespawnDelaySeconds);

    void OnValidate()
    {
        aggroRadius = Mathf.Max(0f, aggroRadius);
        loseAggroRadius = Mathf.Max(aggroRadius, loseAggroRadius);
        attackRange = Mathf.Max(0f, attackRange);
        stopDistance = Mathf.Clamp(stopDistance, 0f, attackRange);
    }
}

using UnityEngine;

/// <summary>敌人追击、攻击与受控时序配置；木桩通过关闭行动开关实现。</summary>
[CreateAssetMenu(fileName = "EnemyBrainProfile", menuName = "ACT/Enemy/Brain Profile")]
public sealed class EnemyBrainProfile : ScriptableObject
{
    [Header("Actions")]
    [Tooltip("关闭后不追击、不攻击（木桩）；仍响应受击/死亡门闩与 Reaction 表现。")]
    [SerializeField] bool enableCombatActions = true;

    [Header("Combat AI")]
    [SerializeField] float aggroRadius = 10f;
    [SerializeField] float loseAggroRadius = 14f;
    [SerializeField] float attackRange = 2f;
    [SerializeField] int attackCooldownFrames = 72;
    [SerializeField, Range(0f, 1f)] float chaseMoveMagnitude = 1f;
    [SerializeField] float stopDistance = 1.2f;
    [SerializeField] int repathIntervalFrames = 6;
    [SerializeField] bool faceTargetWhileChase = true;
    [SerializeField] int failedAttackRetryFrames = 12;
    [SerializeField] float deathDespawnDelaySeconds = 0.5f;

    /// <summary>为 false 时 Brain 不写移动/攻击输入（木桩）；Hit/Death 门闩仍生效。</summary>
    public bool EnableCombatActions => enableCombatActions;

    /// <summary>进入仇恨的水平距离。</summary>
    public float AggroRadius => Mathf.Max(0f, aggroRadius);
    /// <summary>脱离仇恨的水平距离，至少等于进战半径。</summary>
    public float LoseAggroRadius => Mathf.Max(AggroRadius, loseAggroRadius);
    /// <summary>允许请求攻击的水平距离。</summary>
    public float AttackRange => Mathf.Max(0f, attackRange);
    /// <summary>一次成功攻击结束后的冷却逻辑帧数。</summary>
    public int AttackCooldownFrames => Mathf.Max(0, attackCooldownFrames);
    /// <summary>追击时写入移动轴的幅度。</summary>
    public float ChaseMoveMagnitude => Mathf.Clamp01(chaseMoveMagnitude);
    /// <summary>贴近目标后停止移动的距离。</summary>
    public float StopDistance => Mathf.Max(0f, stopDistance);
    /// <summary>刷新假相机朝向的最小逻辑帧间隔。</summary>
    public int RepathIntervalFrames => Mathf.Max(0, repathIntervalFrames);
    /// <summary>追击时是否刷新面向目标的假相机。</summary>
    public bool FaceTargetWhileChase => faceTargetWhileChase;
    /// <summary>攻击起手失败后的防抖逻辑帧数。</summary>
    public int FailedAttackRetryFrames => Mathf.Max(1, failedAttackRetryFrames);
    /// <summary>死亡表现完成后的额外回收等待。</summary>
    public float DeathDespawnDelaySeconds => Mathf.Max(0f, deathDespawnDelaySeconds);

    void OnValidate()
    {
        aggroRadius = Mathf.Max(0f, aggroRadius);
        loseAggroRadius = Mathf.Max(aggroRadius, loseAggroRadius);
        attackRange = Mathf.Max(0f, attackRange);
        stopDistance = Mathf.Clamp(stopDistance, 0f, attackRange);
        attackCooldownFrames = Mathf.Max(0, attackCooldownFrames);
        repathIntervalFrames = Mathf.Max(0, repathIntervalFrames);
        failedAttackRetryFrames = Mathf.Max(1, failedAttackRetryFrames);
    }
}

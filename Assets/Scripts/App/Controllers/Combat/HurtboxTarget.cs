using UnityEngine;

/// <summary>静态受击目标（木桩等）：Inspector 配置 Hurtbox，命中时输出测试日志；同时作为 ITargetable 供索敌。</summary>
public class HurtboxTarget : AppControllerBase, ITargetable
{
    [SerializeField] HurtboxDefinition hurtbox = new();
    [SerializeField] Transform aimPoint = null;
    [SerializeField] int teamId = 1;
    [SerializeField] float currentHealth = float.MaxValue;

    void OnEnable() => GetSystem<TargetSystem>()?.Register(this);

    void OnDisable() => GetSystem<TargetSystem>()?.Unregister(this);

    public int TargetInstanceId => gameObject.GetInstanceID();

    /// <summary>索敌瞄准点；未绑定时回退到自身 Transform。</summary>
    public Transform AimTransform => aimPoint != null ? aimPoint : transform;

    /// <summary>目标是否仍可被索敌与命中。</summary>
    public bool IsAlive => isActiveAndEnabled;

    /// <summary>当前生命值；无血量系统时保持 float.MaxValue。</summary>
    public float CurrentHealth => currentHealth;

    /// <summary>阵营 id；与攻击者 teamId 相同的目标不会被索敌。</summary>
    public int TeamId => teamId;

    /// <summary>Inspector 绑定的受击框数据。</summary>
    public HurtboxDefinition Hurtbox => hurtbox;

    /// <summary>返回当前世界空间受击 OBB。</summary>
    public HitboxOrientedBox GetWorldHurtbox() =>
        HitboxMath.BuildFromHurtbox(transform, hurtbox);

    /// <summary>命中回调；现阶段仅打印测试信息。</summary>
    public void OnHit(in ActionHitContext context)
    {
        Debug.Log("AttackSucc");
    }

    void OnDrawGizmosSelected()
    {
        DrawHurtboxGizmo(new Color(0.2f, 0.85f, 1f, 0.85f));
    }

    /// <summary>绘制受击框线框，供组件 Gizmo 与编辑器复用。</summary>
    public void DrawHurtboxGizmo(Color color)
    {
        HitboxGizmoDrawing.DrawWireOrientedBox(GetWorldHurtbox(), color);
    }
}

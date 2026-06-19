using UnityEngine;

/// <summary>静态受击目标（木桩等）：Inspector 配置 Hurtbox，命中时输出测试日志。</summary>
public class HurtboxTarget : MonoBehaviour, IHurtboxTarget
{
    [SerializeField] HurtboxDefinition hurtbox = new();

    void OnEnable() => HurtboxTargetRegistry.Register(this);

    void OnDisable() => HurtboxTargetRegistry.Unregister(this);

    public int TargetInstanceId => gameObject.GetInstanceID();

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

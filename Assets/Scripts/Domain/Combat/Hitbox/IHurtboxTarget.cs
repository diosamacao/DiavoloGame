/// <summary>可被 Hitbox 命中的目标接口。</summary>
public interface IHurtboxTarget
{
    /// <summary>用于同招防重复命中的实例标识。</summary>
    int TargetInstanceId { get; }

    /// <summary>当前帧的世界空间受击 OBB。</summary>
    HitboxOrientedBox GetWorldHurtbox();

    /// <summary>被命中时回调。</summary>
    void OnHit(in ActionHitContext context);
}

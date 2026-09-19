/// <summary>接收角色硬受击与死亡 Gameplay 边沿；实现不得修改本次裁定结果。</summary>
public interface ICharacterReactionObserver
{
    /// <summary>角色进入硬受击前通知上层 Gameplay。</summary>
    void OnHardHit(in ActionHitContext context);

    /// <summary>角色进入死亡状态前通知上层 Gameplay。</summary>
    void OnDeath(in ActionHitContext context, float damage);
}

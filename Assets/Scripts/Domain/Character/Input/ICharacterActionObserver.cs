/// <summary>接收角色动作起手 Gameplay 边沿；用于上层阵容资源规则，不参与动作裁定。</summary>
public interface ICharacterActionObserver
{
    /// <summary>动作实例成功建立后通知上层。</summary>
    void OnActionBegun(GameplayIntentType intent);
}

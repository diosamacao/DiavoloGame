/// <summary>为位移 Modifier/Command 提供目标逻辑 Pose（禁止读表现骨骼）。</summary>
public interface IActionMotionWorldQuery
{
    /// <summary>按稳定 SimActorId 取已提交战斗 Pose；无效返回 false。</summary>
    bool TryGetCommittedCombatPose(SimActorId actorId, out SimCombatPose pose);
}

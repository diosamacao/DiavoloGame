/// <summary>
/// Locomotion 朝向策略（L-DIR1）：相位无关；替代表达层旧 GaitRotationMode 语义。
/// 枚举值对齐旧 LocomotionRotationMode（FollowInput=1 / FaceCamera=3），便于资产 FormerlySerializedAs 迁移。
/// </summary>
public enum LocomotionFacingMode
{
    /// <summary>朝向追 wish（玩家探索；Motor = FollowInput）。</summary>
    FollowMove = 1,

    /// <summary>面朝锁定目标（L-DIR3 接线；未接线时回退 FollowMove）。</summary>
    FaceTarget = 2,

    /// <summary>面朝相机/假相机前向（敌人对峙；Motor = FaceCamera）。</summary>
    FaceCamera = 3,
}

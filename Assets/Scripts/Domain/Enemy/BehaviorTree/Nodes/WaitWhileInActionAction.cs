using UnityEngine;

/// <summary>
/// 行动：占用 BT 直至离开 Action（含 CombatRequest 当帧与 AttackConfirmPending）。
/// 等待期间清空 MoveDesire，避免对峙侧移污染攻击旋转。
/// </summary>
public sealed class WaitWhileInActionAction : IBehaviorNode
{
    bool _latched;

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard == null)
            return BehaviorStatus.Failure;

        // 起手当帧尚未进 Action：靠 CombatRequest / ConfirmPending 闩住
        bool busy = blackboard.HasCombatRequest
            || blackboard.AttackConfirmPending
            || blackboard.CharacterState == CharacterStateType.Action;

        if (busy)
        {
            _latched = true;
            blackboard.MoveDesire = Vector2.zero;
            blackboard.FaceTargetRequested = true;
            return BehaviorStatus.Running;
        }

        if (_latched)
        {
            // 已进过招并回到可移动态
            _latched = false;
            blackboard.MoveDesire = Vector2.zero;
            return BehaviorStatus.Success;
        }

        // 未闩住且不在 Action：视为空等（脉冲未发出）
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset() => _latched = false;
}

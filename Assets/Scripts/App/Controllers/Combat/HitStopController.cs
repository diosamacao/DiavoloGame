using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 命中卡肉表现：订阅帧末 AttackHitEvent 冻结关联 VFX；时长与 ActionSim.freezeFrames 对齐，
/// 由 SimulationLogicStepEvent 逐逻辑帧递减，不再使用 unscaledDeltaTime。
/// 攻击者骨骼冻结由 CharacterActionPresentationBridge 读 Snapshot.FreezeFrames 驱动。
/// </summary>
[DisallowMultipleComponent]
public class HitStopController : AppControllerBase
{
    Transform _activeAttacker;
    readonly Dictionary<Transform, int> _lastTriggeredActionInstance = new();
    int _remainingFrames;

    void OnEnable()
    {
        RegisterEvent<AttackHitEvent>(HandleAttackHit);
        RegisterEvent<SimulationLogicStepEvent>(HandleLogicStep);
    }

    void OnDisable()
    {
        UnregisterEvent<AttackHitEvent>(HandleAttackHit);
        UnregisterEvent<SimulationLogicStepEvent>(HandleLogicStep);
        ForceEndHitStop();
        _lastTriggeredActionInstance.Clear();
    }

    /// <summary>客机预测卡肉：只冻刀光/VFX，不发 AttackHitEvent、不结算伤害。</summary>
    public void PresentPredicted(Transform attacker, int frames)
    {
        if (attacker == null || frames <= 0)
            return;

        BeginOrExtend(attacker, frames);
    }

    /// <summary>命中回调：开启攻击者侧 VFX 卡肉窗口（逻辑帧数）。</summary>
    void HandleAttackHit(AttackHitEvent hitEvent)
    {
        ActionHitContext context = hitEvent.Context;
        if (context.Action == null || context.Hitbox == null || context.Attacker == null)
            return;

        HitFeedbackSettings feedback = context.Hitbox.Payload.Feedback;
        if (!feedback.UseHitStop)
            return;

        if (feedback.HitStopOncePerAction && context.ActionInstanceId > 0)
        {
            if (_lastTriggeredActionInstance.TryGetValue(
                    context.Attacker,
                    out int consumedInstance)
                && consumedInstance == context.ActionInstanceId)
            {
                return;
            }

            _lastTriggeredActionInstance[context.Attacker] = context.ActionInstanceId;
        }

        int frames = feedback.HitStopFrames;
        if (frames <= 0)
            return;

        BeginOrExtend(context.Attacker, frames);
    }

    /// <summary>每个权威逻辑帧递减剩余卡肉；到 0 时结束 VFX 冻结。</summary>
    void HandleLogicStep(SimulationLogicStepEvent _)
    {
        if (_remainingFrames <= 0)
            return;

        _remainingFrames--;
        if (_remainingFrames <= 0)
            EndHitStop();
    }

    void BeginOrExtend(Transform attacker, int frames)
    {
        bool alreadyActive = _remainingFrames > 0 && _activeAttacker == attacker;
        if (!alreadyActive)
        {
            ForceEndHitStop();
            _activeAttacker = attacker;
            GetSystem<CombatFeedbackSystem>()?.BeginHitStop(attacker);
        }

        _remainingFrames = Mathf.Max(_remainingFrames, frames);
    }

    void EndHitStop()
    {
        _remainingFrames = 0;
        _activeAttacker = null;
        GetSystem<CombatFeedbackSystem>()?.EndHitStop();
    }

    void ForceEndHitStop()
    {
        if (_remainingFrames <= 0 && _activeAttacker == null)
            return;

        EndHitStop();
    }
}

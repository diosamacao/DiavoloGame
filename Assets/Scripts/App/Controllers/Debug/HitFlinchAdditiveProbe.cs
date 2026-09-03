using UnityEngine;

/// <summary>
/// P-HR0 调试探针：只对已有 Actor 调 PlayAdditive，禁止 EnterHit / ActionSim.Stop。
/// </summary>
public static class HitFlinchAdditiveProbe
{
    /// <summary>
    /// 对目标叠一次 Additive。成功返回 true；失败原因写入 error。
    /// </summary>
    public static bool TryPlay(
        CharacterActor actor,
        AnimationClip clip,
        AvatarMask mask,
        float fadeDuration,
        out string error)
    {
        error = null;
        if (actor == null)
        {
            error = "没有目标 Actor。";
            return false;
        }

        if (clip == null)
        {
            error = "未指定 Additive Clip（在 CombatDebugHud 上拖 Hit_Shake）。";
            return false;
        }

        CharacterAnimationService animation = ResolvePresentation(actor);
        if (animation == null || !animation.HasPlayback)
        {
            error = "目标没有可见 Playable（Listen 无头权威需等 Observer Proxy）。";
            return false;
        }

        CharacterStateType state = actor.CurrentState;
        if (state != CharacterStateType.Action && state != CharacterStateType.Locomotion)
        {
            error = $"目标状态是 {state}，探针只接受 Action/Locomotion。";
            return false;
        }

        ActionSimSnapshot action = actor.ActionSim != null ? actor.ActionSim.Snapshot : default;
        LogProbe("before", actor, action, animation.AdditiveWeight);
        animation.PlayAdditive(clip, mask, fadeDuration);
        LogProbe("after", actor, action, animation.AdditiveWeight);
        return true;
    }

    /// <summary>优先玩家 SelectedTarget 对应敌人，否则场上第一个存活且非 Hit 的敌人。</summary>
    public static CharacterActor ResolveTarget(PlayerController player)
    {
        if (player?.Actor != null
            && player.Actor.TryGetSelectedTarget(out ITargetable selected)
            && selected != null)
        {
            CharacterActor fromLock = FindEnemyBySimulationId(selected.SimulationId);
            if (fromLock != null)
                return fromLock;
        }

        EnemyController[] enemies = Object.FindObjectsOfType<EnemyController>();
        for (int i = 0; i < enemies.Length; i++)
        {
            CharacterActor actor = enemies[i] != null ? enemies[i].Actor : null;
            if (actor == null || actor.CurrentState == CharacterStateType.Death)
                continue;
            if (actor.CurrentState == CharacterStateType.Hit)
                continue;
            return actor;
        }

        return player != null ? player.Actor : null;
    }

    /// <summary>
    /// Full 表现用 Actor 自己的 Graph；Listen/Dedicated 权威无头时改用 Observer Proxy。
    /// </summary>
    public static CharacterAnimationService ResolvePresentation(CharacterActor actor)
    {
        if (actor == null)
            return null;

        CharacterAnimationService local = actor.Animation;
        if (local != null && local.HasPlayback)
            return local;

        CombatWorldController world = CombatWorldController.Current;
        if (world != null
            && world.TryGetPresentationAnimation(actor.SimulationId, out CharacterAnimationService proxy)
            && proxy != null
            && proxy.HasPlayback)
        {
            return proxy;
        }

        return local;
    }

    /// <summary>按稳定模拟 Id 找到场上敌人 Actor。</summary>
    public static CharacterActor FindEnemyBySimulationId(SimActorId id)
    {
        if (!id.IsValid)
            return null;

        EnemyController[] enemies = Object.FindObjectsOfType<EnemyController>();
        for (int i = 0; i < enemies.Length; i++)
        {
            CharacterActor actor = enemies[i] != null ? enemies[i].Actor : null;
            if (actor != null && actor.SimulationId.Equals(id))
                return actor;
        }

        return null;
    }

    static void LogProbe(string phase, CharacterActor actor, in ActionSimSnapshot action, float additiveWeight)
    {
        string actionName = action.IsActive && action.Content is Object unity
            ? unity.name
            : "-";
        Debug.Log(
            $"[P-HR0] {phase} state={actor.CurrentState} action={actionName} " +
            $"frame={action.CurrentFrame} additive={additiveWeight:0.00} " +
            $"id=#{actor.SimulationId.Value}");
    }
}

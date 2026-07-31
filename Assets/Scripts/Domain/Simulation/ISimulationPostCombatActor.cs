/// <summary>在全体 Actor Step 与统一 Combat Resolve 后执行同帧动作收尾的可选契约。</summary>
public interface ISimulationPostCombatActor
{
    /// <summary>处理依赖本帧命中结果的自动衔接与动作结束，不得再次生成命中。</summary>
    void ResolvePostCombat(long frameIndex);
}

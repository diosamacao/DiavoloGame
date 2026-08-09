/// <summary>敌人调试派生状态；决策真源为 BehaviorRunner，本枚举仅供 UI/日志。</summary>
public enum EnemyBrainState
{
    Idle = 0,
    Chase = 10,
    Attack = 20,
    Hit = 30,
    Dead = 40,
}

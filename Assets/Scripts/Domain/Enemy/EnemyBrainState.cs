/// <summary>首版敌人决策状态；优先级为 Dead、Hit、Attack、Chase、Idle。</summary>
public enum EnemyBrainState
{
    Idle = 0,
    Chase = 10,
    Attack = 20,
    Hit = 30,
    Dead = 40,
}

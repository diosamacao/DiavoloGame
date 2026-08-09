/// <summary>内置近战/风筝树结构；供 Asset / 测试构建。</summary>
public static class EnemyBehaviorTreePresets
{
    /// <summary>进战追击 + 冷却普攻 + Idle 停步。</summary>
    public static IBehaviorNode BuildMeleeChaseAttack()
    {
        IBehaviorNode attack = new NamedNode(
            "Attack",
            new SequenceNode(
                new HasTargetCondition(),
                new InAttackRangeCondition(),
                new IsCharacterStateCondition(CharacterStateType.Locomotion),
                new CooldownReadyCondition(EnemyCooldownIds.BasicAttack),
                new StopMoveAction(),
                new PulseAttackAction()));

        IBehaviorNode chase = new NamedNode(
            "Chase",
            new SequenceNode(
                new HasTargetCondition(),
                new InCombatAggroCondition(),
                new MoveTowardTargetAction()));

        IBehaviorNode idle = new NamedNode("Idle", new StopMoveAction());
        return new NamedNode("MeleeRoot", new SelectorNode(attack, chase, idle));
    }

    /// <summary>只追不打：用于证明换资产可改策略。</summary>
    public static IBehaviorNode BuildChaseOnly()
    {
        IBehaviorNode chase = new NamedNode(
            "Chase",
            new SequenceNode(
                new HasTargetCondition(),
                new InCombatAggroCondition(),
                new MoveTowardTargetAction()));

        IBehaviorNode idle = new NamedNode("Idle", new StopMoveAction());
        return new NamedNode("ChaseOnlyRoot", new SelectorNode(chase, idle));
    }

    /// <summary>
    /// 风筝：过近后退，过远追击，中间停步；可选侧移由 Custom 扩展。
    /// 默认阈值：≤2.5m 后退，&gt;4m 追击。
    /// </summary>
    public static IBehaviorNode BuildKite()
    {
        IBehaviorNode backOff = new NamedNode(
            "BackOff",
            new SequenceNode(
                new HasTargetCondition(),
                new InCombatAggroCondition(),
                new DistanceLessEqualCondition(2.5f),
                new BackOffFromTargetAction()));

        IBehaviorNode chase = new NamedNode(
            "Chase",
            new SequenceNode(
                new HasTargetCondition(),
                new InCombatAggroCondition(),
                new DistanceGreaterCondition(4f),
                new MoveTowardTargetAction()));

        IBehaviorNode hold = new NamedNode(
            "Hold",
            new SequenceNode(
                new HasTargetCondition(),
                new InCombatAggroCondition(),
                new FaceTargetAction(),
                new StopMoveAction()));

        IBehaviorNode idle = new NamedNode("Idle", new StopMoveAction());
        return new NamedNode("KiteRoot", new SelectorNode(backOff, chase, hold, idle));
    }
}

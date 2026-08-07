/// <summary>单条属性修饰；由 Effect 或测试注入，经 <see cref="ModifierAggregator"/> 聚合。</summary>
public readonly struct AttributeModifier
{
    /// <summary>创建一条修饰。</summary>
    public AttributeModifier(int handle, AttributeId attribute, ModifierOp op, int value)
    {
        Handle = handle;
        Attribute = attribute;
        Op = op;
        Value = value;
    }

    /// <summary>移除用句柄（由 AttributeSet 分配）。</summary>
    public int Handle { get; }

    /// <summary>作用目标属性。</summary>
    public AttributeId Attribute { get; }

    /// <summary>Flat 或 Percent。</summary>
    public ModifierOp Op { get; }

    /// <summary>Flat=milli 加值；Percent=milli 因子（1000=×1）。</summary>
    public int Value { get; }
}

/// <summary>Effect 定义侧的持续修饰描述（无运行时 Handle）。</summary>
public readonly struct EffectModifierSpec
{
    /// <summary>创建一条持续修饰规格。</summary>
    public EffectModifierSpec(AttributeId attribute, ModifierOp op, int value)
    {
        Attribute = attribute;
        Op = op;
        Value = value;
    }

    /// <summary>目标属性。</summary>
    public AttributeId Attribute { get; }

    /// <summary>Flat 或 Percent。</summary>
    public ModifierOp Op { get; }

    /// <summary>与 <see cref="AttributeModifier.Value"/> 同语义。</summary>
    public int Value { get; }
}

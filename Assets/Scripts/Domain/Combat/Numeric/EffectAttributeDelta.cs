/// <summary>Instant / Periodic 对 Attribute Base 的增量（milli；可为负）。</summary>
public readonly struct EffectAttributeDelta
{
    /// <summary>创建一条 Base 增量。</summary>
    public EffectAttributeDelta(AttributeId attribute, int deltaMilli)
    {
        Attribute = attribute;
        DeltaMilli = deltaMilli;
    }

    /// <summary>目标属性。</summary>
    public AttributeId Attribute { get; }

    /// <summary>加到 Base 的 milli 增量。</summary>
    public int DeltaMilli { get; }
}

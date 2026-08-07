/// <summary>属性修饰运算；聚合式 Current = (Base + ΣFlat) × ΠPercent。</summary>
public enum ModifierOp : byte
{
    /// <summary>与 Base 同单位的加值（milli-int）。</summary>
    Flat = 0,
    /// <summary>乘区因子，1000 = ×1.0，1250 = ×1.25。</summary>
    Percent = 1,
}

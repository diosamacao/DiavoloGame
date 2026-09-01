using System;

/// <summary>跨阵容、存档与联网保持稳定的角色内容标识。</summary>
public readonly struct CharacterId : IEquatable<CharacterId>
{
    readonly string value;

    /// <summary>创建经过去空白处理的稳定角色标识。</summary>
    public CharacterId(string value)
    {
        this.value = value?.Trim() ?? string.Empty;
    }

    /// <summary>序列化使用的稳定字符串。</summary>
    public string Value => value ?? string.Empty;

    /// <summary>非空白标识才可进入阵容。</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    /// <summary>按序号字符串比较角色身份。</summary>
    public bool Equals(CharacterId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is CharacterId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>比较两个角色标识是否完全相同。</summary>
    public static bool operator ==(CharacterId left, CharacterId right) => left.Equals(right);

    /// <summary>比较两个角色标识是否不同。</summary>
    public static bool operator !=(CharacterId left, CharacterId right) => !left.Equals(right);
}

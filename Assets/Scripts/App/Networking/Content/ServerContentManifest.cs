using System;
using System.Collections.Generic;
using System.Text;

/// <summary>服务器 Gameplay 内容闭包：指纹只含玩法身份，不含 Model/VFX/Audio。</summary>
public readonly struct ServerContentManifest
{
    /// <summary>由已登记 Registry 计算指纹。</summary>
    public ServerContentManifest(
        int contentVersion,
        string collisionBakeId,
        ContentFingerprint fingerprint)
    {
        ContentVersion = contentVersion;
        CollisionBakeId = collisionBakeId ?? string.Empty;
        Fingerprint = fingerprint;
    }

    /// <summary>房间声明的内容版本号。</summary>
    public int ContentVersion { get; }

    /// <summary>静态碰撞烘焙资产稳定名；空场地为空串。</summary>
    public string CollisionBakeId { get; }

    /// <summary>Gameplay 指纹；Join 双方必须一致。</summary>
    public ContentFingerprint Fingerprint { get; }

    /// <summary>从 Registry 的 Archetype / Action Id 生成指纹；VFX 资产名不进入哈希。</summary>
    public static ServerContentManifest FromRegistry(
        ActContentRegistry content,
        int contentVersion,
        string collisionBakeId)
    {
        if (content == null)
            throw new ArgumentNullException(nameof(content));

        var archetypeIds = new List<int>();
        content.CopyArchetypeIds(archetypeIds);
        var actionIds = new List<int>();
        content.Actions.CopyActionIds(actionIds);
        ContentFingerprint fingerprint = ComputeFingerprint(
            contentVersion,
            collisionBakeId,
            archetypeIds,
            actionIds);
        return new ServerContentManifest(contentVersion, collisionBakeId, fingerprint);
    }

    /// <summary>稳定哈希：版本 + 碰撞 Id + 排序后的原型与动作 Id。</summary>
    public static ContentFingerprint ComputeFingerprint(
        int contentVersion,
        string collisionBakeId,
        IReadOnlyList<int> archetypeIds,
        IReadOnlyList<int> actionIds)
    {
        var builder = new StringBuilder(128);
        builder.Append(contentVersion);
        builder.Append('|');
        builder.Append(collisionBakeId ?? string.Empty);
        builder.Append("|a");
        AppendSorted(builder, archetypeIds);
        builder.Append("|c");
        AppendSorted(builder, actionIds);
        Hash128(builder.ToString(), out ulong high, out ulong low);
        if (high == 0ul && low == 0ul)
            low = 1ul;
        return new ContentFingerprint(high, low);
    }

    static void AppendSorted(StringBuilder builder, IReadOnlyList<int> values)
    {
        if (values == null || values.Count == 0)
            return;

        var copy = new int[values.Count];
        for (int i = 0; i < values.Count; i++)
            copy[i] = values[i];
        Array.Sort(copy);
        for (int i = 0; i < copy.Length; i++)
        {
            builder.Append(',');
            builder.Append(copy[i]);
        }
    }

    static void Hash128(string text, out ulong high, out ulong low)
    {
        unchecked
        {
            ulong h = 14695981039346656037ul;
            for (int i = 0; i < text.Length; i++)
                h = (h ^ text[i]) * 1099511628211ul;
            high = h;
            ulong l = 14695981039346656037ul;
            for (int i = text.Length - 1; i >= 0; i--)
                l = (l ^ text[i]) * 1099511628211ul;
            low = l;
        }
    }
}

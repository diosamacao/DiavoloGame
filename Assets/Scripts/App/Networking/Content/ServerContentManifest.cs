using System;
using System.Collections.Generic;
using System.Text;

/// <summary>服务器 Gameplay 内容闭包：指纹只含玩法身份，不含 Model/VFX/Audio。</summary>
public readonly struct ServerContentManifest
{
    /// <summary>由冻结 Catalog 计算指纹。</summary>
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

    /// <summary>从冻结 Catalog 的 Archetype / Action Id 生成指纹；VFX 资产名不进入哈希。</summary>
    public static ServerContentManifest FromCatalog(
        GameContentCatalog content,
        int contentVersion,
        string collisionBakeId)
    {
        if (content == null)
            throw new ArgumentNullException(nameof(content));
        if (!content.IsFrozen)
            throw new InvalidOperationException("Gameplay 指纹只能从冻结 Content Catalog 生成。");

        var archetypeIds = new List<int>();
        content.CopyArchetypeIds(archetypeIds);
        var actionIds = new List<int>();
        content.Actions.CopyActionIds(actionIds);
        var intentSignatures = new List<string>();
        content.GameplayIntents.CopyStableSignatures(intentSignatures);
        var actions = new List<ActionDefinition>();
        content.Actions.CopyActions(actions);
        actions.Sort((a, b) => content.Actions.RequireId(a).CompareTo(content.Actions.RequireId(b)));
        var collisionSignatures = new List<string>(actions.Count);
        foreach (ActionDefinition action in actions)
        {
            if (!action.ExecutionPolicy.HasValidBodyCollision)
                throw new InvalidOperationException($"Invalid body collision policy: {action.name}");
            collisionSignatures.Add(FormattableString.Invariant(
                $"{content.Actions.RequireId(action)}:{(int)action.ExecutionPolicy.BodyCollisionMode}:{action.ExecutionPolicy.BodyContactSkinMm}"));
            if (action.ExecutionPolicy.UsesInputMovement)
            {
                if (action.GetInputMovementError() != null)
                    throw new InvalidOperationException($"Invalid input movement policy: {action.name}");
                foreach (ActionInputMovement move in action.Timeline.InputMovementStates)
                    collisionSignatures.Add(FormattableString.Invariant(
                        $"input-window:{content.Actions.RequireId(action)}:{move.StartFrame}:{move.EndFrame}:{move.overrideMovementAnimation}:{move.speedMmPerSecond}:{move.inputThreshold:R}:{move.minimumDirectionFrames}"));
            }
        }
        ContentFingerprint fingerprint = ComputeFingerprint(
            contentVersion,
            collisionBakeId,
            archetypeIds,
            actionIds,
            intentSignatures,
            collisionSignatures);
        return new ServerContentManifest(contentVersion, collisionBakeId, fingerprint);
    }

    /// <summary>稳定哈希：版本 + 碰撞 Id + 排序后的原型与动作 Id。</summary>
    public static ContentFingerprint ComputeFingerprint(
        int contentVersion,
        string collisionBakeId,
        IReadOnlyList<int> archetypeIds,
        IReadOnlyList<int> actionIds)
    {
        return ComputeFingerprint(
            contentVersion,
            collisionBakeId,
            archetypeIds,
            actionIds,
            Array.Empty<string>());
    }

    /// <summary>稳定哈希同时覆盖有序 Gameplay Intent 规则，防止两端用不同映射解释同一 InputFrame。</summary>
    public static ContentFingerprint ComputeFingerprint(
        int contentVersion,
        string collisionBakeId,
        IReadOnlyList<int> archetypeIds,
        IReadOnlyList<int> actionIds,
        IReadOnlyList<string> intentSignatures,
        IReadOnlyList<string> bodyCollisionSignatures = null)
    {
        var builder = new StringBuilder(128);
        // 快照增加动作内方向时钟；旧客户端必须在 Join 指纹检查时拒绝。
        builder.Append("action-input-movement:1|");
        builder.Append(contentVersion);
        builder.Append('|');
        builder.Append(collisionBakeId ?? string.Empty);
        builder.Append("|a");
        AppendSorted(builder, archetypeIds);
        builder.Append("|c");
        AppendSorted(builder, actionIds);
        builder.Append("|i");
        AppendOrdered(builder, intentSignatures);
        builder.Append("|body:").Append(ActionBodySweep.RulesVersion);
        AppendOrdered(builder, bodyCollisionSignatures);
        Hash128(builder.ToString(), out ulong high, out ulong low);
        if (high == 0ul && low == 0ul)
            low = 1ul;
        return new ContentFingerprint(high, low);
    }

    static void AppendOrdered(StringBuilder builder, IReadOnlyList<string> values)
    {
        if (values == null)
            return;
        for (int i = 0; i < values.Count; i++)
        {
            builder.Append(',');
            builder.Append(values[i] ?? string.Empty);
        }
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

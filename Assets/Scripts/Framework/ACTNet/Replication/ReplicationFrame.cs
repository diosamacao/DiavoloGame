using System;

/// <summary>承载一个时序复制帧的显式生命周期记录与应用层附加载荷。</summary>
public sealed class ReplicationFrame
{
    readonly SpawnRecord[] _spawns;
    readonly EntityRecord[] _updates;
    readonly DespawnRecord[] _despawns;
    readonly byte[] _applicationPayload;

    /// <summary>该帧对应的权威模拟 Tick。</summary>
    public NetTick Tick { get; }

    /// <summary>该连接上的单调帧序列号。</summary>
    public NetSequence Sequence { get; }

    /// <summary>返回按 EntityId 稳定排序的 Spawn 数组副本。</summary>
    public SpawnRecord[] Spawns => (SpawnRecord[])_spawns.Clone();

    /// <summary>返回按 EntityId 稳定排序的 Update 数组副本。</summary>
    public EntityRecord[] Updates => (EntityRecord[])_updates.Clone();

    /// <summary>返回按 EntityId 稳定排序的 Despawn 数组副本。</summary>
    public DespawnRecord[] Despawns => (DespawnRecord[])_despawns.Clone();

    /// <summary>返回供后续 ACT Adapter 使用的应用层载荷副本。</summary>
    public byte[] ApplicationPayload => Clone(_applicationPayload);

    /// <summary>验证并复制帧的全部输入，记录按 EntityId 排序。</summary>
    public ReplicationFrame(
        NetTick tick,
        NetSequence sequence,
        SpawnRecord[] spawns,
        EntityRecord[] updates,
        DespawnRecord[] despawns,
        byte[] applicationPayload)
    {
        if (!tick.IsValid)
            throw new ArgumentException("ReplicationFrame Tick 必须有效。", nameof(tick));
        if (!sequence.IsValid)
            throw new ArgumentException("ReplicationFrame Sequence 必须有效。", nameof(sequence));
        if (spawns == null)
            throw new ArgumentNullException(nameof(spawns));
        if (updates == null)
            throw new ArgumentNullException(nameof(updates));
        if (despawns == null)
            throw new ArgumentNullException(nameof(despawns));
        if (applicationPayload == null)
            throw new ArgumentNullException(nameof(applicationPayload));

        Tick = tick;
        Sequence = sequence;
        _spawns = CopyAndSort(spawns, item => item?.EntityId.Value ?? int.MinValue, nameof(spawns));
        _updates = CopyAndSort(updates, item => item?.EntityId.Value ?? int.MinValue, nameof(updates));
        _despawns = CopyAndSort(
            despawns,
            item => item?.EntityId.Value ?? int.MinValue,
            nameof(despawns));
        _applicationPayload = Clone(applicationPayload);
    }

    /// <summary>供同程序集 Runtime 和 Codec 按稳定顺序读取，禁止向外暴露可变数组。</summary>
    internal SpawnRecord[] SpawnBuffer => _spawns;

    /// <summary>供同程序集 Runtime 和 Codec 按稳定顺序读取，禁止向外暴露可变数组。</summary>
    internal EntityRecord[] UpdateBuffer => _updates;

    /// <summary>供同程序集 Runtime 和 Codec 按稳定顺序读取，禁止向外暴露可变数组。</summary>
    internal DespawnRecord[] DespawnBuffer => _despawns;

    /// <summary>供同程序集 Codec 读取已隔离的应用载荷。</summary>
    internal byte[] ApplicationPayloadBuffer => _applicationPayload;

    // 复制并检查引用数组后确定性排序，阻断调用方后续替换元素。
    static T[] CopyAndSort<T>(T[] source, Func<T, int> keySelector, string parameterName)
        where T : class
    {
        var copy = (T[])source.Clone();
        for (int i = 0; i < copy.Length; i++)
        {
            if (copy[i] == null)
                throw new ArgumentException("记录数组不能包含 null。", parameterName);
        }

        // 插入排序保留重复 Id 的输入次序，让错误帧的诊断与事件次序仍可复现。
        for (int i = 1; i < copy.Length; i++)
        {
            T current = copy[i];
            int currentKey = keySelector(current);
            int insertion = i;
            while (insertion > 0 && keySelector(copy[insertion - 1]) > currentKey)
            {
                copy[insertion] = copy[insertion - 1];
                insertion--;
            }

            copy[insertion] = current;
        }
        return copy;
    }

    static byte[] Clone(byte[] value)
    {
        if (value.Length == 0)
            return Array.Empty<byte>();

        var copy = new byte[value.Length];
        Buffer.BlockCopy(value, 0, copy, 0, value.Length);
        return copy;
    }
}
